-- 007-idempotencia-concorrente.sql
-- Corrige o reenvio SIMULTÂNEO do mesmo ClientRequestId em sp_IniciarEtapa e sp_FinalizarEtapa.
--
-- A conferência de idempotência do 003 roda antes de travar a etapa: se a mesma requisição chega duas vezes ao
-- mesmo tempo, as duas passam pela conferência, a primeira conclui e a segunda, ao obter o lock, encontrava a
-- etapa já iniciada e falhava com 51005. Agora a conferência é repetida depois do lock e o reenvio termina sem
-- erro, como o reenvio sequencial. O 003 não é alterado: scripts já aplicados são imutáveis.
GO

ALTER PROCEDURE dbo.sp_IniciarEtapa
    @OrdemProducaoEtapaId int,
    @ClientRequestId      uniqueidentifier,
    @DataHoraCliente      datetime2(0)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @dono bit = IIF(@@TRANCOUNT = 0, 1, 0);
    DECLARE @UsuarioId int = TRY_CAST(SESSION_CONTEXT(N'UsuarioId') AS int);

    BEGIN TRY
        IF @UsuarioId IS NULL THROW 51010, N'Usuário não definido no contexto da sessão.', 1;
        IF @dono = 1 BEGIN TRAN;

        -- Idempotência: o mesmo ClientRequestId nunca baixa estoque duas vezes
        IF EXISTS (SELECT 1 FROM dbo.ApontamentoProducao WHERE ClientRequestId = @ClientRequestId)
        BEGIN
            IF @dono = 1 COMMIT;
            RETURN;
        END;

        DECLARE @Status varchar(12), @OpId int, @Seq tinyint, @EstacaoId tinyint;
        SELECT @Status = Status, @OpId = OrdemProducaoId, @Seq = Sequencia, @EstacaoId = EstacaoId
          FROM dbo.OrdemProducaoEtapa WITH (UPDLOCK, ROWLOCK) WHERE OrdemProducaoEtapaId = @OrdemProducaoEtapaId;

        -- Reenvio simultâneo: quem esperava o lock da etapa encontra o apontamento gravado pelo vencedor
        -- e termina sem erro (a primeira conferência, antes do lock, não enxerga uma transação em andamento)
        IF EXISTS (SELECT 1 FROM dbo.ApontamentoProducao WHERE ClientRequestId = @ClientRequestId)
        BEGIN
            IF @dono = 1 COMMIT;
            RETURN;
        END;

        IF @Status IS NULL THROW 51008, N'Etapa não encontrada.', 1;
        IF @Status <> 'Pendente' THROW 51005, N'A etapa não está pendente.', 1;

        DECLARE @OpStatus varchar(12), @ProdutoId int, @OpQtd int;
        SELECT @OpStatus = Status, @ProdutoId = ProdutoId, @OpQtd = Quantidade
          FROM dbo.OrdemProducao WITH (UPDLOCK, ROWLOCK) WHERE OrdemProducaoId = @OpId;
        IF @OpStatus IN ('Concluida', 'Cancelada') THROW 51005, N'A ordem de produção não permite novas etapas.', 1;

        -- RN05: a etapa anterior precisa estar concluída
        IF @Seq > 1 AND NOT EXISTS (SELECT 1 FROM dbo.OrdemProducaoEtapa
                                     WHERE OrdemProducaoId = @OpId AND Sequencia = @Seq - 1 AND Status = 'Concluida')
            THROW 51002, N'Etapa anterior não concluída.', 1;

        -- RN03/RN04: consumo = ficha técnica na estação da etapa x quantidade da OP
        DECLARE @Consumo TABLE (MateriaPrimaId int PRIMARY KEY, Necessario decimal(12,3) NOT NULL);
        INSERT @Consumo (MateriaPrimaId, Necessario)
        SELECT f.MateriaPrimaId, f.Quantidade * @OpQtd
          FROM dbo.FichaTecnicaItem f
         WHERE f.ProdutoId = @ProdutoId AND f.EstacaoId = @EstacaoId;

        -- Passo 1: trava os saldos (UPDLOCK) por chave, SEM filtrar pelo saldo. Com READ_COMMITTED_SNAPSHOT,
        -- um filtro de saldo seria avaliado na versão antiga da linha e o lock não seria tomado.
        DECLARE @Travadas int;
        SELECT @Travadas = COUNT(*)
          FROM @Consumo c
          JOIN dbo.MateriaPrima mp WITH (UPDLOCK, ROWLOCK) ON mp.MateriaPrimaId = c.MateriaPrimaId;

        -- Passo 2: com os locks já obtidos, confere o saldo atual
        DECLARE @Falta nvarchar(400);
        SELECT TOP (1) @Falta = mp.Nome + N': necessário ' + CONVERT(nvarchar(30), c.Necessario)
                              + N' ' + mp.Unidade + N', disponível ' + CONVERT(nvarchar(30), mp.EstoqueAtual)
                              + N' ' + mp.Unidade + N'.'
          FROM @Consumo c
          JOIN dbo.MateriaPrima mp ON mp.MateriaPrimaId = c.MateriaPrimaId
         WHERE mp.EstoqueAtual < c.Necessario
         ORDER BY mp.MateriaPrimaId;

        IF @Falta IS NOT NULL
        BEGIN
            DECLARE @Msg nvarchar(500) = N'Saldo insuficiente de matéria-prima. ' + @Falta;
            THROW 51001, @Msg, 1;
        END;

        INSERT dbo.MovimentoEstoque (MateriaPrimaId, Tipo, Quantidade, Origem, OrigemId, UsuarioId, DataHora)
        SELECT MateriaPrimaId, 'Saida', -Necessario, 'Producao', @OrdemProducaoEtapaId, @UsuarioId, SYSUTCDATETIME()
          FROM @Consumo;

        UPDATE dbo.OrdemProducaoEtapa
           SET Status = 'EmAndamento', OperadorId = @UsuarioId, IniciadaEm = SYSUTCDATETIME()
         WHERE OrdemProducaoEtapaId = @OrdemProducaoEtapaId;

        UPDATE dbo.OrdemProducao SET Status = 'EmAndamento' WHERE OrdemProducaoId = @OpId AND Status = 'Planejada';

        INSERT dbo.ApontamentoProducao (OrdemProducaoEtapaId, Tipo, UsuarioId, DataHoraCliente, ClientRequestId)
        VALUES (@OrdemProducaoEtapaId, 'Inicio', @UsuarioId, @DataHoraCliente, @ClientRequestId);

        IF @dono = 1 COMMIT;
    END TRY
    BEGIN CATCH
        IF @dono = 1 AND @@TRANCOUNT > 0 ROLLBACK;
        -- Reenvio simultâneo do mesmo ClientRequestId: o outro processamento venceu; nada a fazer
        IF @dono = 1 AND ERROR_NUMBER() IN (2601, 2627)
           AND EXISTS (SELECT 1 FROM dbo.ApontamentoProducao WHERE ClientRequestId = @ClientRequestId) RETURN;
        THROW;
    END CATCH
END;
GO

ALTER PROCEDURE dbo.sp_FinalizarEtapa
    @OrdemProducaoEtapaId int,
    @ClientRequestId      uniqueidentifier,
    @DataHoraCliente      datetime2(0)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @dono bit = IIF(@@TRANCOUNT = 0, 1, 0);
    DECLARE @UsuarioId int = TRY_CAST(SESSION_CONTEXT(N'UsuarioId') AS int);

    BEGIN TRY
        IF @UsuarioId IS NULL THROW 51010, N'Usuário não definido no contexto da sessão.', 1;
        IF @dono = 1 BEGIN TRAN;

        IF EXISTS (SELECT 1 FROM dbo.ApontamentoProducao WHERE ClientRequestId = @ClientRequestId)
        BEGIN
            IF @dono = 1 COMMIT;
            RETURN;
        END;

        DECLARE @Status varchar(12), @EstacaoNome varchar(40);
        SELECT @Status = e.Status, @EstacaoNome = es.Nome
          FROM dbo.OrdemProducaoEtapa e WITH (UPDLOCK, ROWLOCK)
          JOIN dbo.EstacaoProducao es ON es.EstacaoId = e.EstacaoId
         WHERE e.OrdemProducaoEtapaId = @OrdemProducaoEtapaId;

        -- Reenvio simultâneo: quem esperava o lock da etapa encontra o apontamento gravado pelo vencedor
        -- e termina sem erro (a primeira conferência, antes do lock, não enxerga uma transação em andamento)
        IF EXISTS (SELECT 1 FROM dbo.ApontamentoProducao WHERE ClientRequestId = @ClientRequestId)
        BEGIN
            IF @dono = 1 COMMIT;
            RETURN;
        END;

        IF @Status IS NULL THROW 51008, N'Etapa não encontrada.', 1;
        IF @Status <> 'EmAndamento' THROW 51005, N'A etapa não está em andamento.', 1;
        IF @EstacaoNome = 'Qualidade'
            THROW 51005, N'A etapa de Qualidade só é concluída por inspeção.', 1;

        UPDATE dbo.OrdemProducaoEtapa
           SET Status = 'Concluida', ConcluidaEm = SYSUTCDATETIME()
         WHERE OrdemProducaoEtapaId = @OrdemProducaoEtapaId;

        INSERT dbo.ApontamentoProducao (OrdemProducaoEtapaId, Tipo, UsuarioId, DataHoraCliente, ClientRequestId)
        VALUES (@OrdemProducaoEtapaId, 'Fim', @UsuarioId, @DataHoraCliente, @ClientRequestId);

        IF @dono = 1 COMMIT;
    END TRY
    BEGIN CATCH
        IF @dono = 1 AND @@TRANCOUNT > 0 ROLLBACK;
        IF @dono = 1 AND ERROR_NUMBER() IN (2601, 2627)
           AND EXISTS (SELECT 1 FROM dbo.ApontamentoProducao WHERE ClientRequestId = @ClientRequestId) RETURN;
        THROW;
    END CATCH
END;
GO
