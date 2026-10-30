namespace Aurora.Web.Api.Pedidos;

/// <summary>Corpo de criação e de edição do pedido; na edição os itens substituem os atuais.</summary>
public sealed record PedidoRequisicao(int ClienteId, DateOnly PrazoEntrega, string? Observacao, IReadOnlyList<PedidoItemRequisicao> Itens);
