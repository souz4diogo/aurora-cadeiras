-- 003-procedures.sql
-- Operações atômicas de estoque e produção (docs/05-banco-de-dados.md §6 e §7).
--
-- Códigos de erro (THROW):
--   51001 RN04  saldo insuficiente            51006 RN06  inspeção inválida
--   51002 RN05  etapa anterior não concluída  51007 -     recebimento acima do comprado
--   51003 RN01  pedido não confirmado         51008 -     registro não encontrado
--   51004 RN07  pedido não está pronto        51009 -     registro imutável (trigger)
--   51005 -     status não permite a operação 51010 -     parâmetro inválido / contexto ausente
--
-- Padrão: SET XACT_ABORT ON; transação própria só se não houver uma aberta (@dono);
-- usuário lido de SESSION_CONTEXT(N'UsuarioId').
GO

-- 7.2 Gera OPs a partir de um pedido confirmado (RN01, RN02) ------------------
CREATE PROCEDURE dbo.sp_GerarOrdensProducao
    @PedidoId int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @dono bit = IIF(@@TRANCOUNT = 0, 1, 0);

    BEGIN TRY
        IF @dono = 1 BEGIN TRAN;

        DECLARE @Status varchar(15), @Prazo date;
        SELECT @Status = Status, @Prazo = PrazoEntrega
          FROM dbo.Pedido WITH (UPDLOCK, ROWLOCK) WHERE PedidoId = @PedidoId;

        IF @Status IS NULL THROW 51008, N'Pedido não encontrado.', 1;
        IF @Status <> 'Confirmado' THROW 51003, N'Pedido não está confirmado.', 1;

        -- Garante a linha de estoque de cada produto do pedido
        -- A criação é serializada por um lock de aplicação (evita deadlock de key-range e PK duplicada)
        IF EXISTS (SELECT 1 FROM dbo.PedidoItem pi
                    WHERE pi.PedidoId = @PedidoId
                      AND NOT EXISTS (SELECT 1 FROM dbo.EstoqueProduto ep WHERE ep.ProdutoId = pi.ProdutoId))
        BEGIN
            DECLARE @Lock int;
            EXEC @Lock = sp_getapplock @Resource = N'Aurora.EstoqueProduto.Criar', @LockMode = 'Exclusive',
                                       @LockOwner = 'Transaction', @LockTimeout = 10000;
            IF @Lock < 0 THROW 51005, N'Não foi possível reservar a criação do estoque do produto. Tente novamente.', 1;

            INSERT dbo.EstoqueProduto (ProdutoId, Quantidade, Reservado)
            SELECT pi.ProdutoId, 0, 0
              FROM dbo.PedidoItem pi
             WHERE pi.PedidoId = @PedidoId
               AND NOT EXISTS (SELECT 1 FROM dbo.EstoqueProduto ep WHERE ep.ProdutoId = pi.ProdutoId);
        END;

        DECLARE @ItemId int, @ProdutoId int, @Qtd int, @Disponivel int, @Reservar int, @Produzir int, @OpId int;
        DECLARE @CriouOp bit = 0;

        DECLARE itens CURSOR LOCAL FAST_FORWARD FOR
            SELECT PedidoItemId, ProdutoId, Quantidade FROM dbo.PedidoItem WHERE PedidoId = @PedidoId ORDER BY PedidoItemId;
        OPEN itens;
        FETCH NEXT FROM itens INTO @ItemId, @ProdutoId, @Qtd;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            -- RN02: reserva produto acabado disponível em vez de gerar OP
            SELECT @Disponivel = Quantidade - Reservado
              FROM dbo.EstoqueProduto WITH (UPDLOCK, ROWLOCK) WHERE ProdutoId = @ProdutoId;

            SET @Reservar = IIF(@Qtd < @Disponivel, @Qtd, @Disponivel);
            IF @Reservar > 0
                UPDATE dbo.EstoqueProduto SET Reservado = Reservado + @Reservar WHERE ProdutoId = @ProdutoId;

            SET @Produzir = @Qtd - @Reservar;
            IF @Produzir > 0
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM dbo.RoteiroProduto r
                                 JOIN dbo.EstacaoProducao e ON e.EstacaoId = r.EstacaoId
                                WHERE r.ProdutoId = @ProdutoId AND e.GeraEtapa = 1)
                    THROW 51005, N'Produto sem roteiro de produção cadastrado.', 1;

                INSERT dbo.OrdemProducao (PedidoItemId, ProdutoId, Quantidade, Status, PrevisaoConclusao)
                VALUES (@ItemId, @ProdutoId, @Produzir, 'Planejada', DATEADD(DAY, -2, @Prazo));
                SET @OpId = SCOPE_IDENTITY();

                INSERT dbo.OrdemProducaoEtapa (OrdemProducaoId, EstacaoId, Sequencia, Status)
                SELECT @OpId, r.EstacaoId,
                       ROW_NUMBER() OVER (ORDER BY r.Sequencia),
                       'Pendente'
                  FROM dbo.RoteiroProduto r
                  JOIN dbo.EstacaoProducao e ON e.EstacaoId = r.EstacaoId
                 WHERE r.ProdutoId = @ProdutoId AND e.GeraEtapa = 1;

                SET @CriouOp = 1;
            END;

            FETCH NEXT FROM itens INTO @ItemId, @ProdutoId, @Qtd;
        END;
        CLOSE itens;
        DEALLOCATE itens;

        UPDATE dbo.Pedido SET Status = IIF(@CriouOp = 1, 'EmProducao', 'Pronto') WHERE PedidoId = @PedidoId;

        IF @dono = 1 COMMIT;
    END TRY
    BEGIN CATCH
        IF @dono = 1 AND @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO

-- 7.1 Confirma o pedido e gera as OPs ---------------------------------------
CREATE PROCEDURE dbo.sp_ConfirmarPedido
    @PedidoId int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @dono bit = IIF(@@TRANCOUNT = 0, 1, 0);

    BEGIN TRY
        IF @dono = 1 BEGIN TRAN;

        DECLARE @Status varchar(15);
        SELECT @Status = Status FROM dbo.Pedido WITH (UPDLOCK, ROWLOCK) WHERE PedidoId = @PedidoId;

        IF @Status IS NULL THROW 51008, N'Pedido não encontrado.', 1;
        IF @Status <> 'Aberto' THROW 51005, N'Somente pedidos abertos podem ser confirmados.', 1;
        IF NOT EXISTS (SELECT 1 FROM dbo.PedidoItem WHERE PedidoId = @PedidoId)
            THROW 51005, N'O pedido não possui itens.', 1;

        UPDATE dbo.Pedido SET Status = 'Confirmado' WHERE PedidoId = @PedidoId;
        EXEC dbo.sp_GerarOrdensProducao @PedidoId = @PedidoId;

        IF @dono = 1 COMMIT;
    END TRY
    BEGIN CATCH
        IF @dono = 1 AND @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO

-- 7.3 Inicia a etapa e baixa a matéria-prima (RN03, RN04, RN05) --------------
CREATE PROCEDURE dbo.sp_IniciarEtapa
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

-- 7.4 Finaliza a etapa (a Qualidade só conclui por inspeção) -----------------
CREATE PROCEDURE dbo.sp_FinalizarEtapa
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

-- 7.5 Inspeção de qualidade (RN06) -------------------------------------------
CREATE PROCEDURE dbo.sp_RegistrarInspecao
    @OrdemProducaoId     int,
    @Resultado           varchar(10),
    @EstacaoRetrabalhoId tinyint       = NULL,
    @Observacao          nvarchar(300) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @dono bit = IIF(@@TRANCOUNT = 0, 1, 0);
    DECLARE @UsuarioId int = TRY_CAST(SESSION_CONTEXT(N'UsuarioId') AS int);

    BEGIN TRY
        IF @UsuarioId IS NULL THROW 51010, N'Usuário não definido no contexto da sessão.', 1;
        IF @Resultado NOT IN ('Aprovada', 'Reprovada') THROW 51006, N'Resultado de inspeção inválido.', 1;
        IF @Resultado = 'Reprovada' AND @EstacaoRetrabalhoId IS NULL
            THROW 51006, N'Inspeção reprovada exige a estação de retrabalho.', 1;
        IF @dono = 1 BEGIN TRAN;

        DECLARE @OpStatus varchar(12), @ProdutoId int, @OpQtd int, @PedidoId int;
        SELECT @OpStatus = op.Status, @ProdutoId = op.ProdutoId, @OpQtd = op.Quantidade, @PedidoId = pi.PedidoId
          FROM dbo.OrdemProducao op WITH (UPDLOCK, ROWLOCK)
          JOIN dbo.PedidoItem pi ON pi.PedidoItemId = op.PedidoItemId
         WHERE op.OrdemProducaoId = @OrdemProducaoId;

        IF @OpStatus IS NULL THROW 51008, N'Ordem de produção não encontrada.', 1;
        IF @OpStatus IN ('Concluida', 'Cancelada') THROW 51005, N'A ordem de produção não permite inspeção.', 1;

        DECLARE @QualEtapaId int, @QualSeq tinyint, @QualStatus varchar(12);
        SELECT @QualEtapaId = e.OrdemProducaoEtapaId, @QualSeq = e.Sequencia, @QualStatus = e.Status
          FROM dbo.OrdemProducaoEtapa e WITH (UPDLOCK, ROWLOCK)
          JOIN dbo.EstacaoProducao es ON es.EstacaoId = e.EstacaoId
         WHERE e.OrdemProducaoId = @OrdemProducaoId AND es.Nome = 'Qualidade';

        IF @QualEtapaId IS NULL THROW 51005, N'A ordem de produção não tem etapa de Qualidade.', 1;
        IF @QualStatus <> 'EmAndamento' THROW 51005, N'A etapa de Qualidade não está em andamento.', 1;
        IF EXISTS (SELECT 1 FROM dbo.OrdemProducaoEtapa
                    WHERE OrdemProducaoId = @OrdemProducaoId AND Sequencia < @QualSeq AND Status <> 'Concluida')
            THROW 51002, N'Etapa anterior não concluída.', 1;

        INSERT dbo.Inspecao (OrdemProducaoId, InspetorId, Resultado, EstacaoRetrabalhoId, Observacao)
        VALUES (@OrdemProducaoId, @UsuarioId, @Resultado, @EstacaoRetrabalhoId, @Observacao);

        IF @Resultado = 'Aprovada'
        BEGIN
            UPDATE dbo.OrdemProducaoEtapa
               SET Status = 'Concluida', ConcluidaEm = SYSUTCDATETIME()
             WHERE OrdemProducaoEtapaId = @QualEtapaId;

            UPDATE dbo.OrdemProducao
               SET Status = 'Concluida', ConcluidaEm = SYSUTCDATETIME()
             WHERE OrdemProducaoId = @OrdemProducaoId;

            -- Entrada de produto acabado (o trigger soma em EstoqueProduto) e reserva para o pedido
            INSERT dbo.MovimentoEstoque (ProdutoId, Tipo, Quantidade, Origem, OrigemId, UsuarioId)
            VALUES (@ProdutoId, 'Entrada', @OpQtd, 'ProducaoConcluida', @OrdemProducaoId, @UsuarioId);

            UPDATE dbo.EstoqueProduto SET Reservado = Reservado + @OpQtd WHERE ProdutoId = @ProdutoId;

            -- Pedido pronto quando nenhuma OP dele está pendente de produção
            IF NOT EXISTS (SELECT 1
                             FROM dbo.OrdemProducao op
                             JOIN dbo.PedidoItem pi ON pi.PedidoItemId = op.PedidoItemId
                            WHERE pi.PedidoId = @PedidoId AND op.Status IN ('Planejada', 'EmAndamento'))
                UPDATE dbo.Pedido SET Status = 'Pronto' WHERE PedidoId = @PedidoId AND Status = 'EmProducao';
        END
        ELSE
        BEGIN
            DECLARE @RetSeq tinyint;
            SELECT @RetSeq = Sequencia FROM dbo.OrdemProducaoEtapa
             WHERE OrdemProducaoId = @OrdemProducaoId AND EstacaoId = @EstacaoRetrabalhoId;

            IF @RetSeq IS NULL OR @RetSeq >= @QualSeq
                THROW 51006, N'Estação de retrabalho inválida para esta ordem de produção.', 1;

            DECLARE @RetEtapaId int;
            SELECT @RetEtapaId = OrdemProducaoEtapaId FROM dbo.OrdemProducaoEtapa
             WHERE OrdemProducaoId = @OrdemProducaoId AND Sequencia = @RetSeq;

            -- Reabre as etapas da estação de retrabalho até a Qualidade
            UPDATE dbo.OrdemProducaoEtapa
               SET Status = 'Pendente', IniciadaEm = NULL, ConcluidaEm = NULL, OperadorId = NULL
             WHERE OrdemProducaoId = @OrdemProducaoId AND Sequencia >= @RetSeq;

            INSERT dbo.RegistroPerda (OrdemProducaoEtapaId, Tipo, Quantidade, Motivo, UsuarioId)
            VALUES (@RetEtapaId, 'Retrabalho', @OpQtd, ISNULL(@Observacao, N'Reprovada na inspeção de qualidade'), @UsuarioId);

            UPDATE dbo.OrdemProducao SET Status = 'EmAndamento' WHERE OrdemProducaoId = @OrdemProducaoId;
        END;

        IF @dono = 1 COMMIT;
    END TRY
    BEGIN CATCH
        IF @dono = 1 AND @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO

-- 7.6 Recebimento de compra (RF17) -------------------------------------------
CREATE PROCEDURE dbo.sp_ReceberItemCompra
    @PedidoCompraItemId int,
    @Quantidade         decimal(12,3)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @dono bit = IIF(@@TRANCOUNT = 0, 1, 0);
    DECLARE @UsuarioId int = TRY_CAST(SESSION_CONTEXT(N'UsuarioId') AS int);

    BEGIN TRY
        IF @Quantidade IS NULL OR @Quantidade <= 0 THROW 51010, N'A quantidade recebida deve ser maior que zero.', 1;
        IF @dono = 1 BEGIN TRAN;

        DECLARE @PedidoCompraId int, @MpId int, @QtdComprada decimal(12,3), @QtdRecebida decimal(12,3), @Custo decimal(12,4);
        SELECT @PedidoCompraId = PedidoCompraId, @MpId = MateriaPrimaId, @QtdComprada = Quantidade,
               @QtdRecebida = QuantidadeRecebida, @Custo = CustoUnitario
          FROM dbo.PedidoCompraItem WITH (UPDLOCK, ROWLOCK) WHERE PedidoCompraItemId = @PedidoCompraItemId;

        IF @PedidoCompraId IS NULL THROW 51008, N'Item de compra não encontrado.', 1;

        DECLARE @StatusCompra varchar(22);
        SELECT @StatusCompra = Status FROM dbo.PedidoCompra WITH (UPDLOCK, ROWLOCK) WHERE PedidoCompraId = @PedidoCompraId;
        IF @StatusCompra NOT IN ('Emitido', 'ParcialmenteRecebido')
            THROW 51005, N'O pedido de compra não está emitido para recebimento.', 1;

        IF @QtdRecebida + @Quantidade > @QtdComprada
            THROW 51007, N'Recebimento acima da quantidade comprada.', 1;

        UPDATE dbo.PedidoCompraItem SET QuantidadeRecebida = QuantidadeRecebida + @Quantidade
         WHERE PedidoCompraItemId = @PedidoCompraItemId;

        INSERT dbo.MovimentoEstoque (MateriaPrimaId, Tipo, Quantidade, Origem, OrigemId, UsuarioId)
        VALUES (@MpId, 'Entrada', @Quantidade, 'Compra', @PedidoCompraId, @UsuarioId);

        UPDATE dbo.MateriaPrima SET CustoUnitario = @Custo WHERE MateriaPrimaId = @MpId;

        UPDATE dbo.PedidoCompra
           SET Status = IIF(NOT EXISTS (SELECT 1 FROM dbo.PedidoCompraItem
                                         WHERE PedidoCompraId = @PedidoCompraId AND QuantidadeRecebida < Quantidade),
                            'Recebido', 'ParcialmenteRecebido')
         WHERE PedidoCompraId = @PedidoCompraId;

        IF @dono = 1 COMMIT;
    END TRY
    BEGIN CATCH
        IF @dono = 1 AND @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO

-- 7.7 Ajuste de estoque (novo movimento; o livro-razão não se altera) --------
CREATE PROCEDURE dbo.sp_AjustarEstoque
    @MateriaPrimaId int,
    @NovaQuantidade decimal(12,3),
    @Motivo         nvarchar(200)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @dono bit = IIF(@@TRANCOUNT = 0, 1, 0);
    DECLARE @UsuarioId int = TRY_CAST(SESSION_CONTEXT(N'UsuarioId') AS int);

    BEGIN TRY
        IF LTRIM(RTRIM(ISNULL(@Motivo, N''))) = N'' THROW 51010, N'O motivo do ajuste é obrigatório.', 1;
        IF @NovaQuantidade IS NULL OR @NovaQuantidade < 0 THROW 51010, N'A nova quantidade não pode ser negativa.', 1;
        IF @dono = 1 BEGIN TRAN;

        DECLARE @Atual decimal(12,3);
        SELECT @Atual = EstoqueAtual FROM dbo.MateriaPrima WITH (UPDLOCK, ROWLOCK) WHERE MateriaPrimaId = @MateriaPrimaId;
        IF @Atual IS NULL THROW 51008, N'Matéria-prima não encontrada.', 1;

        DECLARE @Diferenca decimal(12,3) = @NovaQuantidade - @Atual;
        IF @Diferenca <> 0
            INSERT dbo.MovimentoEstoque (MateriaPrimaId, Tipo, Quantidade, Origem, UsuarioId, Motivo)
            VALUES (@MateriaPrimaId, 'Ajuste', @Diferenca, 'Ajuste', @UsuarioId, @Motivo);

        IF @dono = 1 COMMIT;
    END TRY
    BEGIN CATCH
        IF @dono = 1 AND @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO

-- 7.8 Expedição (RN07) -------------------------------------------------------
CREATE PROCEDURE dbo.sp_Expedir
    @PedidoId       int,
    @Transportadora nvarchar(80) = NULL,
    @CodigoRastreio varchar(40)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @dono bit = IIF(@@TRANCOUNT = 0, 1, 0);
    DECLARE @UsuarioId int = TRY_CAST(SESSION_CONTEXT(N'UsuarioId') AS int);

    BEGIN TRY
        IF @UsuarioId IS NULL THROW 51010, N'Usuário não definido no contexto da sessão.', 1;
        IF @dono = 1 BEGIN TRAN;

        DECLARE @Status varchar(15);
        SELECT @Status = Status FROM dbo.Pedido WITH (UPDLOCK, ROWLOCK) WHERE PedidoId = @PedidoId;
        IF @Status IS NULL THROW 51008, N'Pedido não encontrado.', 1;
        IF @Status <> 'Pronto' THROW 51004, N'Pedido não está pronto para expedição.', 1;

        -- Primeiro libera a reserva e só depois baixa a quantidade (respeita CK Reservado <= Quantidade)
        UPDATE ep
           SET Reservado = ep.Reservado - pi.Quantidade
          FROM dbo.EstoqueProduto ep WITH (UPDLOCK, ROWLOCK)
          JOIN dbo.PedidoItem pi ON pi.ProdutoId = ep.ProdutoId
         WHERE pi.PedidoId = @PedidoId;

        INSERT dbo.MovimentoEstoque (ProdutoId, Tipo, Quantidade, Origem, OrigemId, UsuarioId)
        SELECT ProdutoId, 'Saida', -Quantidade, 'Expedicao', @PedidoId, @UsuarioId
          FROM dbo.PedidoItem WHERE PedidoId = @PedidoId;

        INSERT dbo.Expedicao (PedidoId, UsuarioId, Transportadora, CodigoRastreio)
        VALUES (@PedidoId, @UsuarioId, @Transportadora, @CodigoRastreio);

        UPDATE dbo.Pedido SET Status = 'Expedido' WHERE PedidoId = @PedidoId;

        IF @dono = 1 COMMIT;
    END TRY
    BEGIN CATCH
        IF @dono = 1 AND @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO

-- 7.9 Cancelamento de pedido -------------------------------------------------
CREATE PROCEDURE dbo.sp_CancelarPedido
    @PedidoId int
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    DECLARE @dono bit = IIF(@@TRANCOUNT = 0, 1, 0);

    BEGIN TRY
        IF @dono = 1 BEGIN TRAN;

        DECLARE @Status varchar(15);
        SELECT @Status = Status FROM dbo.Pedido WITH (UPDLOCK, ROWLOCK) WHERE PedidoId = @PedidoId;
        IF @Status IS NULL THROW 51008, N'Pedido não encontrado.', 1;
        IF @Status NOT IN ('Aberto', 'Confirmado', 'EmProducao')
            THROW 51005, N'O status atual do pedido não permite o cancelamento.', 1;

        IF @Status <> 'Aberto'
        BEGIN
            -- Reservado por item = quantidade do item menos o que ainda está em produção
            UPDATE ep
               SET Reservado = ep.Reservado - r.Reservado
              FROM dbo.EstoqueProduto ep WITH (UPDLOCK, ROWLOCK)
              JOIN (SELECT pi.ProdutoId,
                           pi.Quantidade - ISNULL(SUM(CASE WHEN op.Status IN ('Planejada', 'EmAndamento') THEN op.Quantidade END), 0) AS Reservado
                      FROM dbo.PedidoItem pi
                      LEFT JOIN dbo.OrdemProducao op ON op.PedidoItemId = pi.PedidoItemId
                     WHERE pi.PedidoId = @PedidoId
                     GROUP BY pi.PedidoItemId, pi.ProdutoId, pi.Quantidade) r ON r.ProdutoId = ep.ProdutoId
             WHERE r.Reservado > 0;

            UPDATE op SET Status = 'Cancelada'
              FROM dbo.OrdemProducao op
              JOIN dbo.PedidoItem pi ON pi.PedidoItemId = op.PedidoItemId
             WHERE pi.PedidoId = @PedidoId AND op.Status IN ('Planejada', 'EmAndamento');
        END;

        UPDATE dbo.Pedido SET Status = 'Cancelado' WHERE PedidoId = @PedidoId;

        IF @dono = 1 COMMIT;
    END TRY
    BEGIN CATCH
        IF @dono = 1 AND @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO
