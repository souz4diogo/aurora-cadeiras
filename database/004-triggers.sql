-- 004-triggers.sql
-- Efeitos automáticos: saldo, imutabilidade do livro-razão, alerta de mínimo e auditoria
-- (docs/05-banco-de-dados.md §8).
GO

-- Aplica o movimento (com sinal) no saldo. Set-based: funciona com várias linhas.
CREATE TRIGGER dbo.trg_MovimentoEstoque_AI ON dbo.MovimentoEstoque AFTER INSERT AS
BEGIN
    SET NOCOUNT ON;

    UPDATE mp
       SET EstoqueAtual = mp.EstoqueAtual + s.Total
      FROM dbo.MateriaPrima mp
      JOIN (SELECT MateriaPrimaId, SUM(Quantidade) AS Total
              FROM inserted
             WHERE MateriaPrimaId IS NOT NULL
             GROUP BY MateriaPrimaId) s ON s.MateriaPrimaId = mp.MateriaPrimaId;

    MERGE dbo.EstoqueProduto WITH (HOLDLOCK) AS alvo
    USING (SELECT ProdutoId, CAST(SUM(Quantidade) AS int) AS Total
             FROM inserted
            WHERE ProdutoId IS NOT NULL
            GROUP BY ProdutoId) AS s
       ON alvo.ProdutoId = s.ProdutoId
    WHEN MATCHED THEN
        UPDATE SET Quantidade = alvo.Quantidade + s.Total
    WHEN NOT MATCHED THEN
        INSERT (ProdutoId, Quantidade, Reservado) VALUES (s.ProdutoId, s.Total, 0);
END;
GO

-- Livro-razão imutável: correções são novos movimentos de ajuste.
CREATE TRIGGER dbo.trg_MovimentoEstoque_Imutavel ON dbo.MovimentoEstoque INSTEAD OF UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    THROW 51009, N'MovimentoEstoque é imutável: registre um novo movimento de ajuste.', 1;
END;
GO

-- RN08: saldo no mínimo ou abaixo gera alerta (um alerta aberto por material).
CREATE TRIGGER dbo.trg_MateriaPrima_AU_Alerta ON dbo.MateriaPrima AFTER UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF NOT (UPDATE(EstoqueAtual) OR UPDATE(EstoqueMinimo)) RETURN;

    INSERT dbo.AlertaEstoque (MateriaPrimaId)
    SELECT i.MateriaPrimaId
      FROM inserted i
     WHERE i.Ativo = 1
       AND i.EstoqueAtual <= i.EstoqueMinimo
       AND NOT EXISTS (SELECT 1 FROM dbo.AlertaEstoque a
                        WHERE a.MateriaPrimaId = i.MateriaPrimaId AND a.LidoEm IS NULL);
END;
GO

-- Auditoria (RNF10): antes/depois em JSON; o usuário vem de SESSION_CONTEXT.
CREATE TRIGGER dbo.trg_Auditoria_Pedido ON dbo.Pedido AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UsuarioId int = TRY_CAST(SESSION_CONTEXT(N'UsuarioId') AS int);

    INSERT dbo.Auditoria (Tabela, Registro, Acao, UsuarioId, Antes, Depois)
    SELECT N'Pedido', CAST(k.Id AS varchar(50)),
           CASE WHEN i.PedidoId IS NULL THEN 'D' WHEN d.PedidoId IS NULL THEN 'I' ELSE 'U' END,
           @UsuarioId,
           (SELECT x.* FROM deleted x WHERE x.PedidoId = k.Id FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
           (SELECT x.* FROM inserted x WHERE x.PedidoId = k.Id FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
      FROM (SELECT PedidoId AS Id FROM inserted UNION SELECT PedidoId FROM deleted) k
      LEFT JOIN inserted i ON i.PedidoId = k.Id
      LEFT JOIN deleted d  ON d.PedidoId = k.Id;
END;
GO

CREATE TRIGGER dbo.trg_Auditoria_OrdemProducao ON dbo.OrdemProducao AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UsuarioId int = TRY_CAST(SESSION_CONTEXT(N'UsuarioId') AS int);

    INSERT dbo.Auditoria (Tabela, Registro, Acao, UsuarioId, Antes, Depois)
    SELECT N'OrdemProducao', CAST(k.Id AS varchar(50)),
           CASE WHEN i.OrdemProducaoId IS NULL THEN 'D' WHEN d.OrdemProducaoId IS NULL THEN 'I' ELSE 'U' END,
           @UsuarioId,
           (SELECT x.* FROM deleted x WHERE x.OrdemProducaoId = k.Id FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
           (SELECT x.* FROM inserted x WHERE x.OrdemProducaoId = k.Id FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
      FROM (SELECT OrdemProducaoId AS Id FROM inserted UNION SELECT OrdemProducaoId FROM deleted) k
      LEFT JOIN inserted i ON i.OrdemProducaoId = k.Id
      LEFT JOIN deleted d  ON d.OrdemProducaoId = k.Id;
END;
GO

CREATE TRIGGER dbo.trg_Auditoria_MateriaPrima ON dbo.MateriaPrima AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UsuarioId int = TRY_CAST(SESSION_CONTEXT(N'UsuarioId') AS int);

    INSERT dbo.Auditoria (Tabela, Registro, Acao, UsuarioId, Antes, Depois)
    SELECT N'MateriaPrima', CAST(k.Id AS varchar(50)),
           CASE WHEN i.MateriaPrimaId IS NULL THEN 'D' WHEN d.MateriaPrimaId IS NULL THEN 'I' ELSE 'U' END,
           @UsuarioId,
           (SELECT x.* FROM deleted x WHERE x.MateriaPrimaId = k.Id FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
           (SELECT x.* FROM inserted x WHERE x.MateriaPrimaId = k.Id FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
      FROM (SELECT MateriaPrimaId AS Id FROM inserted UNION SELECT MateriaPrimaId FROM deleted) k
      LEFT JOIN inserted i ON i.MateriaPrimaId = k.Id
      LEFT JOIN deleted d  ON d.MateriaPrimaId = k.Id;
END;
GO

-- Nunca grava SenhaHash: lista explícita de colunas.
CREATE TRIGGER dbo.trg_Auditoria_Usuario ON dbo.Usuario AFTER INSERT, UPDATE, DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @UsuarioId int = TRY_CAST(SESSION_CONTEXT(N'UsuarioId') AS int);

    INSERT dbo.Auditoria (Tabela, Registro, Acao, UsuarioId, Antes, Depois)
    SELECT N'Usuario', CAST(k.Id AS varchar(50)),
           CASE WHEN i.UsuarioId IS NULL THEN 'D' WHEN d.UsuarioId IS NULL THEN 'I' ELSE 'U' END,
           @UsuarioId,
           (SELECT x.UsuarioId, x.Nome, x.Email, x.PerfilId, x.ClienteId, x.Ativo, x.TentativasFalhas, x.BloqueadoAte
              FROM deleted x WHERE x.UsuarioId = k.Id FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
           (SELECT x.UsuarioId, x.Nome, x.Email, x.PerfilId, x.ClienteId, x.Ativo, x.TentativasFalhas, x.BloqueadoAte
              FROM inserted x WHERE x.UsuarioId = k.Id FOR JSON PATH, WITHOUT_ARRAY_WRAPPER)
      FROM (SELECT UsuarioId AS Id FROM inserted UNION SELECT UsuarioId FROM deleted) k
      LEFT JOIN inserted i ON i.UsuarioId = k.Id
      LEFT JOIN deleted d  ON d.UsuarioId = k.Id;
END;
GO
