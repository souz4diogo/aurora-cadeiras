using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Compras;

public interface IComprasApi
{
    Task<Pagina<PedidoCompraResumoResposta>> ListarAsync(FiltroDePedidosCompra filtro, int pagina, CancellationToken ct = default);

    Task<PedidoCompraResposta> ObterAsync(int id, CancellationToken ct = default);

    Task<PedidoCompraResposta> CriarAsync(PedidoCompraRequisicao dados, CancellationToken ct = default);

    Task<PedidoCompraResposta> EmitirAsync(int id, CancellationToken ct = default);

    Task<PedidoCompraResposta> CancelarAsync(int id, CancellationToken ct = default);

    /// <summary>Registra a chegada (total ou parcial) de um item: a API dá entrada no estoque na mesma transação.</summary>
    Task<PedidoCompraResposta> ReceberItemAsync(int itemId, decimal quantidade, CancellationToken ct = default);
}
