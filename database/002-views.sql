-- 002-views.sql
-- Views de leitura para painel, alertas e relatórios (docs/05-banco-de-dados.md §5).
GO

CREATE VIEW dbo.vw_EstoqueCritico AS
SELECT
    mp.MateriaPrimaId,
    mp.Codigo,
    mp.Nome,
    mp.Unidade,
    mp.EstoqueAtual,
    mp.EstoqueMinimo,
    f.FornecedorId AS FornecedorPreferencialId,
    f.RazaoSocial  AS FornecedorPreferencial
FROM dbo.MateriaPrima mp
LEFT JOIN dbo.Fornecedor f ON f.FornecedorId = mp.FornecedorPreferencialId
WHERE mp.Ativo = 1
  AND mp.EstoqueAtual <= mp.EstoqueMinimo;
GO

CREATE VIEW dbo.vw_PainelProducao AS
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
    CAST(CASE WHEN op.PrevisaoConclusao < CAST(SYSUTCDATETIME() AS date) THEN 1 ELSE 0 END AS bit) AS Atrasada
FROM dbo.OrdemProducaoEtapa e
JOIN dbo.OrdemProducao op ON op.OrdemProducaoId = e.OrdemProducaoId
JOIN dbo.Produto p        ON p.ProdutoId = op.ProdutoId
JOIN dbo.EstacaoProducao es ON es.EstacaoId = e.EstacaoId
WHERE op.Status IN ('Planejada', 'EmAndamento')
  AND e.Status IN ('Pendente', 'EmAndamento');
GO

CREATE VIEW dbo.vw_PedidosAtrasados AS
SELECT
    pd.PedidoId,
    pd.Numero,
    pd.ClienteId,
    c.Nome AS Cliente,
    pd.VendedorId,
    pd.PrazoEntrega,
    pd.Status,
    pd.ValorTotal,
    DATEDIFF(DAY, pd.PrazoEntrega, CAST(SYSUTCDATETIME() AS date)) AS DiasAtraso
FROM dbo.Pedido pd
JOIN dbo.Cliente c ON c.ClienteId = pd.ClienteId
WHERE pd.Status IN ('Confirmado', 'EmProducao', 'Pronto')
  AND pd.PrazoEntrega < CAST(SYSUTCDATETIME() AS date);
GO

-- Linha do tempo do pedido (RF08, RF22): registro, etapas iniciadas/concluídas e expedição.
CREATE VIEW dbo.vw_AcompanhamentoPedido AS
SELECT pd.PedidoId, pd.DataPedido AS DataHora, CAST(N'Pedido registrado' AS nvarchar(200)) AS Evento, CAST(NULL AS varchar(15)) AS NumeroOp
FROM dbo.Pedido pd
UNION ALL
SELECT pi.PedidoId, e.IniciadaEm, N'Etapa iniciada: ' + es.Nome, op.Numero
FROM dbo.OrdemProducaoEtapa e
JOIN dbo.OrdemProducao op   ON op.OrdemProducaoId = e.OrdemProducaoId
JOIN dbo.PedidoItem pi      ON pi.PedidoItemId = op.PedidoItemId
JOIN dbo.EstacaoProducao es ON es.EstacaoId = e.EstacaoId
WHERE e.IniciadaEm IS NOT NULL
UNION ALL
SELECT pi.PedidoId, e.ConcluidaEm, N'Etapa concluída: ' + es.Nome, op.Numero
FROM dbo.OrdemProducaoEtapa e
JOIN dbo.OrdemProducao op   ON op.OrdemProducaoId = e.OrdemProducaoId
JOIN dbo.PedidoItem pi      ON pi.PedidoItemId = op.PedidoItemId
JOIN dbo.EstacaoProducao es ON es.EstacaoId = e.EstacaoId
WHERE e.ConcluidaEm IS NOT NULL
UNION ALL
SELECT pi.PedidoId, op.ConcluidaEm, N'Ordem de produção concluída', op.Numero
FROM dbo.OrdemProducao op
JOIN dbo.PedidoItem pi ON pi.PedidoItemId = op.PedidoItemId
WHERE op.ConcluidaEm IS NOT NULL
UNION ALL
SELECT x.PedidoId, x.DataHora, N'Pedido expedido', NULL
FROM dbo.Expedicao x;
GO

-- Consumo de matéria-prima pela produção, por material e produto.
CREATE VIEW dbo.vw_ConsumoMateriaPrima AS
SELECT
    m.MovimentoEstoqueId,
    m.DataHora,
    mp.MateriaPrimaId,
    mp.Codigo AS CodigoMateriaPrima,
    mp.Nome   AS MateriaPrima,
    mp.Unidade,
    op.ProdutoId,
    p.Codigo  AS CodigoProduto,
    -m.Quantidade AS QuantidadeConsumida,
    -m.Quantidade * mp.CustoUnitario AS CustoEstimado
FROM dbo.MovimentoEstoque m
JOIN dbo.MateriaPrima mp          ON mp.MateriaPrimaId = m.MateriaPrimaId
LEFT JOIN dbo.OrdemProducaoEtapa e ON e.OrdemProducaoEtapaId = m.OrigemId
LEFT JOIN dbo.OrdemProducao op     ON op.OrdemProducaoId = e.OrdemProducaoId
LEFT JOIN dbo.Produto p            ON p.ProdutoId = op.ProdutoId
WHERE m.Origem = 'Producao';
GO

-- Etapa 3: compras recebidas por mês e participação de fornecedores de comunidades tradicionais.
CREATE VIEW dbo.vw_ComprasComunidadesTradicionais AS
WITH Recebimentos AS (
    SELECT
        YEAR(m.DataHora)  AS Ano,
        MONTH(m.DataHora) AS Mes,
        f.ComunidadeTradicional,
        m.Quantidade * pci.CustoUnitario AS Valor
    FROM dbo.MovimentoEstoque m
    JOIN dbo.PedidoCompraItem pci ON pci.PedidoCompraId = m.OrigemId AND pci.MateriaPrimaId = m.MateriaPrimaId
    JOIN dbo.PedidoCompra pc      ON pc.PedidoCompraId = pci.PedidoCompraId
    JOIN dbo.Fornecedor f         ON f.FornecedorId = pc.FornecedorId
    WHERE m.Origem = 'Compra'
)
SELECT
    Ano,
    Mes,
    CAST(SUM(CASE WHEN ComunidadeTradicional = 1 THEN Valor ELSE 0 END) AS decimal(14,2)) AS ValorComunidades,
    CAST(SUM(Valor) AS decimal(14,2)) AS ValorTotal,
    CAST(CASE WHEN SUM(Valor) = 0 THEN 0
              ELSE 100.0 * SUM(CASE WHEN ComunidadeTradicional = 1 THEN Valor ELSE 0 END) / SUM(Valor) END AS decimal(5,2)) AS PercentualComunidades
FROM Recebimentos
GROUP BY Ano, Mes;
GO
