-- 009-acompanhamento-sequencia.sql
-- Expõe a sequência da etapa na linha do tempo do pedido (vw_AcompanhamentoPedido).
--
-- As horas são guardadas em segundos (datetime2(0)). Quando duas etapas da mesma OP iniciam e terminam no mesmo
-- segundo, a API só tinha o texto do evento para desempatar e mostrava a "Montagem Final" antes da "Montagem da
-- Estrutura" (ordem alfabética). Com a sequência do roteiro, o desempate segue a ordem real do processo.
-- O 002 não é alterado: scripts já aplicados são imutáveis (CLAUDE.md). ALTER VIEW mantém as permissões (005).
GO

ALTER VIEW dbo.vw_AcompanhamentoPedido AS
SELECT pd.PedidoId, pd.DataPedido AS DataHora, CAST(N'Pedido registrado' AS nvarchar(200)) AS Evento,
       CAST(NULL AS varchar(15)) AS NumeroOp, CAST(NULL AS tinyint) AS Sequencia
FROM dbo.Pedido pd
UNION ALL
SELECT pi.PedidoId, e.IniciadaEm, N'Etapa iniciada: ' + es.Nome, op.Numero, e.Sequencia
FROM dbo.OrdemProducaoEtapa e
JOIN dbo.OrdemProducao op   ON op.OrdemProducaoId = e.OrdemProducaoId
JOIN dbo.PedidoItem pi      ON pi.PedidoItemId = op.PedidoItemId
JOIN dbo.EstacaoProducao es ON es.EstacaoId = e.EstacaoId
WHERE e.IniciadaEm IS NOT NULL
UNION ALL
SELECT pi.PedidoId, e.ConcluidaEm, N'Etapa concluída: ' + es.Nome, op.Numero, e.Sequencia
FROM dbo.OrdemProducaoEtapa e
JOIN dbo.OrdemProducao op   ON op.OrdemProducaoId = e.OrdemProducaoId
JOIN dbo.PedidoItem pi      ON pi.PedidoItemId = op.PedidoItemId
JOIN dbo.EstacaoProducao es ON es.EstacaoId = e.EstacaoId
WHERE e.ConcluidaEm IS NOT NULL
UNION ALL
SELECT pi.PedidoId, op.ConcluidaEm, N'Ordem de produção concluída', op.Numero, NULL
FROM dbo.OrdemProducao op
JOIN dbo.PedidoItem pi ON pi.PedidoItemId = op.PedidoItemId
WHERE op.ConcluidaEm IS NOT NULL
UNION ALL
SELECT x.PedidoId, x.DataHora, N'Pedido expedido', NULL, NULL
FROM dbo.Expedicao x;
GO
