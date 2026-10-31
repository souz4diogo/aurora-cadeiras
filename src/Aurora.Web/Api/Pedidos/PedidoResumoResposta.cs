namespace Aurora.Web.Api.Pedidos;

public sealed record PedidoResumoResposta(
    int Id,
    string Numero,
    int ClienteId,
    string ClienteNome,
    int VendedorId,
    DateTime DataPedido,
    DateOnly PrazoEntrega,
    string Status,
    decimal ValorTotal,
    int QuantidadeItens,
    bool Atrasado);
