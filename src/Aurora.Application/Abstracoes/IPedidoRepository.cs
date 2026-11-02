using Aurora.Application.Pedidos;
using Aurora.Domain.Entidades;

namespace Aurora.Application.Abstracoes;

public interface IPedidoRepository
{
    /// <summary>Pedido com os itens, rastreado para edição.</summary>
    Task<Pedido?> ObterPorIdAsync(int id, CancellationToken ct = default);

    Task<(IReadOnlyList<PedidoResumo> Itens, int Total)> ListarAsync(
        FiltroDePedidos filtro, int pagina, int tamanho, CancellationToken ct = default);

    /// <summary>Linha do tempo do pedido (vw_AcompanhamentoPedido), do evento mais antigo ao mais recente.</summary>
    Task<IReadOnlyList<EventoDoPedidoDto>> ListarEventosAsync(int pedidoId, CancellationToken ct = default);

    void Adicionar(Pedido pedido);

    /// <summary>Confirma pelo sp_ConfirmarPedido (reserva estoque ou gera OPs) e atualiza o pedido rastreado.</summary>
    Task ConfirmarAsync(int pedidoId, CancellationToken ct = default);

    /// <summary>Cancela pelo sp_CancelarPedido (libera reservas e cancela OPs) e atualiza o pedido rastreado.</summary>
    Task CancelarAsync(int pedidoId, CancellationToken ct = default);
}
