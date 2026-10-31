using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Producao;

public interface IProducaoApi
{
    Task<Pagina<OrdemProducaoResumoResposta>> ListarOrdensAsync(FiltroDeOrdens filtro, int pagina, CancellationToken ct = default);

    /// <summary>As OPs em aberto agrupadas pela estação em que estão agora (quadro de produção).</summary>
    Task<QuadroDeProducaoResposta> ObterQuadroAsync(CancellationToken ct = default);

    Task<OrdemProducaoResposta> ObterOrdemAsync(int id, CancellationToken ct = default);

    /// <summary>Todas as OPs de um pedido (um pedido tem poucas: no máximo uma por item).</summary>
    Task<IReadOnlyList<OrdemProducaoResumoResposta>> ListarOrdensDoPedidoAsync(int pedidoId, CancellationToken ct = default);
}
