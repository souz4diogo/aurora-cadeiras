namespace Aurora.Web.Api.Pedidos;

/// <summary>Um marco da linha do tempo do pedido (criação, confirmação, início/fim de OP, expedição...).</summary>
public sealed record EventoDoPedidoResposta(DateTime DataHora, string Evento, string? NumeroOp);

public sealed record AcompanhamentoResposta(PedidoResposta Pedido, IReadOnlyList<EventoDoPedidoResposta> Eventos);
