using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Pedidos;

public interface IPedidosApi
{
    Task<Pagina<PedidoResumoResposta>> ListarAsync(FiltroDePedidos filtro, int pagina, CancellationToken ct = default);

    Task<PedidoResposta> ObterAsync(int id, CancellationToken ct = default);

    Task<PedidoResposta> CriarAsync(PedidoRequisicao dados, CancellationToken ct = default);

    Task<PedidoResposta> AtualizarAsync(int id, PedidoRequisicao dados, CancellationToken ct = default);

    Task<PedidoResposta> CancelarAsync(int id, CancellationToken ct = default);
}
