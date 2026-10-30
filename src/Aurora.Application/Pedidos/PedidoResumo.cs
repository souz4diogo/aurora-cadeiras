using Aurora.Domain.Enums;

namespace Aurora.Application.Pedidos;

/// <summary>Linha da listagem de pedidos (leitura): já traz o nome do cliente e a contagem de itens.</summary>
public sealed record PedidoResumo(
    int Id,
    string Numero,
    int ClienteId,
    string ClienteNome,
    int VendedorId,
    DateTime DataPedido,
    DateOnly PrazoEntrega,
    StatusPedido Status,
    decimal ValorTotal,
    int QuantidadeItens)
{
    /// <summary>Mesma regra da view vw_PedidosAtrasados: em andamento e com o prazo vencido.</summary>
    public bool EstaAtrasado(DateOnly hoje) =>
        Status is StatusPedido.Confirmado or StatusPedido.EmProducao or StatusPedido.Pronto && PrazoEntrega < hoje;
}
