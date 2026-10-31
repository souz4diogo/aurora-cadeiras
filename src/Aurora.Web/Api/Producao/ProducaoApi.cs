using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Producao;

public sealed class ProducaoApi(HttpClient http) : ApiClientBase(http), IProducaoApi
{
    public Task<Pagina<OrdemProducaoResumoResposta>> ListarOrdensAsync(FiltroDeOrdens filtro, int pagina, CancellationToken ct = default) =>
        HttpGetAsync<Pagina<OrdemProducaoResumoResposta>>(
            "api/v1/ordens-producao" + Consulta(
                ("status", filtro.Status), ("estacaoId", filtro.EstacaoId), ("pedidoId", filtro.PedidoId),
                ("atrasadas", filtro.Atrasadas), ("page", pagina), ("pageSize", TamanhoDePagina.Padrao)),
            ct);

    public Task<QuadroDeProducaoResposta> ObterQuadroAsync(CancellationToken ct = default) =>
        HttpGetAsync<QuadroDeProducaoResposta>("api/v1/ordens-producao/quadro", ct);

    public Task<OrdemProducaoResposta> ObterOrdemAsync(int id, CancellationToken ct = default) =>
        HttpGetAsync<OrdemProducaoResposta>($"api/v1/ordens-producao/{id}", ct);

    public Task<IReadOnlyList<OrdemProducaoResumoResposta>> ListarOrdensDoPedidoAsync(int pedidoId, CancellationToken ct = default) =>
        TodasAsPaginasAsync<OrdemProducaoResumoResposta>("api/v1/ordens-producao", [("pedidoId", pedidoId)], ct);
}
