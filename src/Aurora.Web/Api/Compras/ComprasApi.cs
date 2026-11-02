using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Compras;

public sealed class ComprasApi(HttpClient http) : ApiClientBase(http), IComprasApi
{
    public Task<Pagina<PedidoCompraResumoResposta>> ListarAsync(
        FiltroDePedidosCompra filtro, int pagina, CancellationToken ct = default) =>
        HttpGetAsync<Pagina<PedidoCompraResumoResposta>>(
            "api/v1/pedidos-compra" + Consulta(
                ("status", filtro.Status), ("fornecedorId", filtro.FornecedorId), ("de", filtro.De), ("ate", filtro.Ate),
                ("atrasados", filtro.Atrasados), ("page", pagina), ("pageSize", TamanhoDePagina.Padrao)),
            ct);

    public Task<PedidoCompraResposta> ObterAsync(int id, CancellationToken ct = default) =>
        HttpGetAsync<PedidoCompraResposta>($"api/v1/pedidos-compra/{id}", ct);

    public Task<PedidoCompraResposta> CriarAsync(PedidoCompraRequisicao dados, CancellationToken ct = default) =>
        HttpPostAsync<PedidoCompraResposta>("api/v1/pedidos-compra", dados, ct);

    public Task<PedidoCompraResposta> EmitirAsync(int id, CancellationToken ct = default) =>
        HttpPostAsync<PedidoCompraResposta>($"api/v1/pedidos-compra/{id}/emitir", new { }, ct);

    public Task<PedidoCompraResposta> CancelarAsync(int id, CancellationToken ct = default) =>
        HttpPostAsync<PedidoCompraResposta>($"api/v1/pedidos-compra/{id}/cancelar", new { }, ct);

    public Task<PedidoCompraResposta> ReceberItemAsync(int itemId, decimal quantidade, CancellationToken ct = default) =>
        HttpPostAsync<PedidoCompraResposta>($"api/v1/pedidos-compra/itens/{itemId}/receber", new ReceberItemRequisicao(quantidade), ct);
}
