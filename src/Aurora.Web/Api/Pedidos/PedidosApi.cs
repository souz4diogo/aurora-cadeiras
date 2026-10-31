using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Pedidos;

public sealed class PedidosApi(HttpClient http) : ApiClientBase(http), IPedidosApi
{
    public Task<Pagina<PedidoResumoResposta>> ListarAsync(FiltroDePedidos filtro, int pagina, CancellationToken ct = default) =>
        HttpGetAsync<Pagina<PedidoResumoResposta>>(
            "api/v1/pedidos" + Consulta(
                ("status", filtro.Status), ("clienteId", filtro.ClienteId), ("de", filtro.De), ("ate", filtro.Ate),
                ("atrasados", filtro.Atrasados), ("page", pagina), ("pageSize", TamanhoDePagina.Padrao)),
            ct);

    public Task<PedidoResposta> ObterAsync(int id, CancellationToken ct = default) =>
        HttpGetAsync<PedidoResposta>($"api/v1/pedidos/{id}", ct);

    public Task<PedidoResposta> CriarAsync(PedidoRequisicao dados, CancellationToken ct = default) =>
        HttpPostAsync<PedidoResposta>("api/v1/pedidos", dados, ct);

    public Task<PedidoResposta> AtualizarAsync(int id, PedidoRequisicao dados, CancellationToken ct = default) =>
        HttpPutAsync<PedidoResposta>($"api/v1/pedidos/{id}", dados, ct);

    public Task<PedidoResposta> ConfirmarAsync(int id, CancellationToken ct = default) =>
        HttpPostAsync<PedidoResposta>($"api/v1/pedidos/{id}/confirmar", new { }, ct);

    public Task<PedidoResposta> CancelarAsync(int id, CancellationToken ct = default) =>
        HttpPostAsync<PedidoResposta>($"api/v1/pedidos/{id}/cancelar", new { }, ct);
}
