-- 008-dia-de-negocio.sql
-- Corrige o "hoje" dos atrasos nas views: era a data em UTC, e agora é a de Brasília (UTC-3), a mesma da API.
--
-- Com a data em UTC, entre 21h e 24h (Brasília) tudo o que vence naquele dia aparecia como atrasado no painel e na
-- lista de pedidos atrasados. O Brasil não tem horário de verão desde 2019, então o deslocamento fixo de -3 horas
-- dispensa tabela de fusos. O 002 não é alterado: scripts já aplicados são imutáveis (CLAUDE.md).
GO

ALTER VIEW dbo.vw_PainelProducao AS
SELECT
    e.OrdemProducaoEtapaId,
    op.OrdemProducaoId,
    op.Numero          AS NumeroOp,
    op.Quantidade,
    p.Codigo           AS CodigoProduto,
    p.Nome             AS Produto,
    e.EstacaoId,
    es.Nome            AS Estacao,
    e.Sequencia,
    e.Status           AS StatusEtapa,
    e.IniciadaEm,
    CASE WHEN e.Status = 'EmAndamento'
         THEN DATEDIFF(MINUTE, e.IniciadaEm, SYSUTCDATETIME()) END AS MinutosNaEtapa,
    op.PrevisaoConclusao,
    CAST(CASE WHEN op.PrevisaoConclusao < CAST(DATEADD(HOUR, -3, SYSUTCDATETIME()) AS date) THEN 1 ELSE 0 END AS bit) AS Atrasada
FROM dbo.OrdemProducaoEtapa e
JOIN dbo.OrdemProducao op ON op.OrdemProducaoId = e.OrdemProducaoId
JOIN dbo.Produto p        ON p.ProdutoId = op.ProdutoId
JOIN dbo.EstacaoProducao es ON es.EstacaoId = e.EstacaoId
WHERE op.Status IN ('Planejada', 'EmAndamento')
  AND e.Status IN ('Pendente', 'EmAndamento');
GO

ALTER VIEW dbo.vw_PedidosAtrasados AS
SELECT
    pd.PedidoId,
    pd.Numero,
    pd.ClienteId,
    c.Nome AS Cliente,
    pd.VendedorId,
    pd.PrazoEntrega,
    pd.Status,
    pd.ValorTotal,
    DATEDIFF(DAY, pd.PrazoEntrega, CAST(DATEADD(HOUR, -3, SYSUTCDATETIME()) AS date)) AS DiasAtraso
FROM dbo.Pedido pd
JOIN dbo.Cliente c ON c.ClienteId = pd.ClienteId
WHERE pd.Status IN ('Confirmado', 'EmProducao', 'Pronto')
  AND pd.PrazoEntrega < CAST(DATEADD(HOUR, -3, SYSUTCDATETIME()) AS date);
GO
