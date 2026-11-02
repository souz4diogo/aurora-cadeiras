using Aurora.Application.Abstracoes;
using Aurora.Application.Painel;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

/// <summary>Painel gerencial sobre as views do banco (vw_PedidosAtrasados, vw_PainelProducao, vw_EstoqueCritico...).</summary>
public sealed class PainelRepository(AuroraDbContext db) : IPainelRepository
{
    public async Task<ResumoDoPainelDto> ObterResumoAsync(DateTime agoraUtc, CancellationToken ct = default)
    {
        // "Hoje" dos atrasos é o do banco (UTC), o mesmo das views; tudo numa ida só ao servidor
        var linha = await db.Database.SqlQuery<LinhaDoResumo>($"""
            SELECT
              (SELECT COUNT(*) FROM dbo.Pedido WHERE Status IN ('Aberto', 'Confirmado', 'EmProducao', 'Pronto')) AS PedidosEmAberto,
              (SELECT COUNT(*) FROM dbo.vw_PedidosAtrasados) AS PedidosAtrasados,
              (SELECT COUNT(*) FROM dbo.Pedido WHERE Status = 'Pronto') AS PedidosProntosParaExpedir,
              (SELECT COUNT(*) FROM dbo.OrdemProducao WHERE Status = 'Planejada') AS OrdensPlanejadas,
              (SELECT COUNT(*) FROM dbo.OrdemProducao WHERE Status = 'EmAndamento') AS OrdensEmAndamento,
              (SELECT COUNT(*) FROM dbo.OrdemProducao
                WHERE Status IN ('Planejada', 'EmAndamento') AND PrevisaoConclusao < CAST(SYSUTCDATETIME() AS date)) AS OrdensAtrasadas,
              (SELECT COUNT(*) FROM dbo.vw_EstoqueCritico) AS MateriaisCriticos,
              (SELECT COUNT(*) FROM dbo.PedidoCompra WHERE Status IN ('Emitido', 'ParcialmenteRecebido')) AS ComprasAReceber,
              (SELECT COUNT(*) FROM dbo.PedidoCompra
                WHERE Status IN ('Emitido', 'ParcialmenteRecebido') AND DataPrevista < CAST(SYSUTCDATETIME() AS date)) AS ComprasAtrasadas
            """).SingleAsync(ct);

        return new ResumoDoPainelDto(
            linha.PedidosEmAberto,
            linha.PedidosAtrasados,
            linha.PedidosProntosParaExpedir,
            linha.OrdensPlanejadas,
            linha.OrdensEmAndamento,
            linha.OrdensAtrasadas,
            linha.MateriaisCriticos,
            linha.ComprasAReceber,
            linha.ComprasAtrasadas,
            agoraUtc);
    }

    public async Task<IReadOnlyList<ProducaoDaEstacaoDto>> ListarProducaoPorEstacaoAsync(CancellationToken ct = default)
    {
        // A estação de uma OP é a da primeira etapa ainda não concluída (a view só tem etapas abertas, então é a de
        // menor sequência): a mesma leitura do quadro de produção
        var linhas = await db.Database.SqlQuery<LinhaDaEstacao>($"""
            WITH Atual AS (
              SELECT *, ROW_NUMBER() OVER (PARTITION BY OrdemProducaoId ORDER BY Sequencia) AS Posicao
                FROM dbo.vw_PainelProducao
            )
            SELECT a.EstacaoId,
                   a.Estacao,
                   es.Ordem,
                   COUNT(*) AS OrdensNaEstacao,
                   SUM(CASE WHEN a.StatusEtapa = 'EmAndamento' THEN 1 ELSE 0 END) AS EmAndamento,
                   SUM(CASE WHEN a.StatusEtapa = 'Pendente'    THEN 1 ELSE 0 END) AS Aguardando,
                   SUM(CAST(a.Atrasada AS int)) AS Atrasadas,
                   AVG(CAST(a.MinutosNaEtapa AS float)) AS TempoMedioMinutos
              FROM Atual a
              JOIN dbo.EstacaoProducao es ON es.EstacaoId = a.EstacaoId
             WHERE a.Posicao = 1
             GROUP BY a.EstacaoId, a.Estacao, es.Ordem
             ORDER BY es.Ordem
            """).ToListAsync(ct);

        return linhas
            .Select(l => new ProducaoDaEstacaoDto(
                l.EstacaoId, l.Estacao, l.Ordem, l.OrdensNaEstacao, l.EmAndamento, l.Aguardando, l.Atrasadas, l.TempoMedioMinutos))
            .ToList();
    }

    public async Task<(IReadOnlyList<PedidoAtrasadoDto> Itens, int Total)> ListarPedidosAtrasadosAsync(
        int? vendedorId, int pagina, int tamanho, CancellationToken ct = default)
    {
        var pular = (pagina - 1) * tamanho;
        var total = await db.Database.SqlQuery<int>(
                $"SELECT COUNT(*) AS [Value] FROM dbo.vw_PedidosAtrasados WHERE ({vendedorId} IS NULL OR VendedorId = {vendedorId})")
            .SingleAsync(ct);
        var linhas = await db.Database.SqlQuery<LinhaDoPedido>($"""
            SELECT PedidoId, Numero, Cliente, PrazoEntrega, Status, ValorTotal, DiasAtraso
              FROM dbo.vw_PedidosAtrasados
             WHERE ({vendedorId} IS NULL OR VendedorId = {vendedorId})
             ORDER BY DiasAtraso DESC, PedidoId
            OFFSET {pular} ROWS FETCH NEXT {tamanho} ROWS ONLY
            """).ToListAsync(ct);

        var itens = linhas
            .Select(l => new PedidoAtrasadoDto(l.PedidoId, l.Numero, l.Cliente, l.PrazoEntrega, l.Status, l.ValorTotal, l.DiasAtraso))
            .ToList();
        return (itens, total);
    }

    public Task<IReadOnlyList<ConsumoDeMaterialDto>> ListarConsumoAsync(
        DateOnly de, DateOnly ate, int limite, CancellationToken ct = default) =>
        ConsultasDeConsumo.PorMateriaAsync(db, de, ate, limite, ct);

    // O SqlQuery preenche estas classes pelo nome das colunas
    private sealed class LinhaDoResumo
    {
        public int PedidosEmAberto { get; set; }

        public int PedidosAtrasados { get; set; }

        public int PedidosProntosParaExpedir { get; set; }

        public int OrdensPlanejadas { get; set; }

        public int OrdensEmAndamento { get; set; }

        public int OrdensAtrasadas { get; set; }

        public int MateriaisCriticos { get; set; }

        public int ComprasAReceber { get; set; }

        public int ComprasAtrasadas { get; set; }
    }

    private sealed class LinhaDaEstacao
    {
        public byte EstacaoId { get; set; }

        public string Estacao { get; set; } = "";

        public byte Ordem { get; set; }

        public int OrdensNaEstacao { get; set; }

        public int EmAndamento { get; set; }

        public int Aguardando { get; set; }

        public int Atrasadas { get; set; }

        public double? TempoMedioMinutos { get; set; }
    }

    private sealed class LinhaDoPedido
    {
        public int PedidoId { get; set; }

        public string Numero { get; set; } = "";

        public string Cliente { get; set; } = "";

        public DateOnly PrazoEntrega { get; set; }

        public string Status { get; set; } = "";

        public decimal ValorTotal { get; set; }

        public int DiasAtraso { get; set; }
    }
}
