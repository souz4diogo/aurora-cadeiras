namespace Aurora.Application.Pedidos;

/// <summary>Um marco da linha do tempo (view vw_AcompanhamentoPedido): registro, etapas, OP concluída e expedição.</summary>
public sealed record EventoDoPedidoDto(DateTime DataHora, string Evento, string? NumeroOp);

/// <summary>Pedido com os itens e a linha do tempo da produção, na ordem em que aconteceu (RF08, RF22).</summary>
public sealed record AcompanhamentoDto(PedidoDto Pedido, IReadOnlyList<EventoDoPedidoDto> Eventos);
