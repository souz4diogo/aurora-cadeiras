using Aurora.Domain.Entidades;

namespace Aurora.Application.Pedidos;

public sealed record PedidoItemRequest(int ProdutoId, int Quantidade);

/// <summary>Corpo de criação (POST) e de edição (PUT) do pedido; o PUT substitui os itens inteiros.</summary>
public sealed record PedidoRequest(int ClienteId, DateOnly PrazoEntrega, string? Observacao, IReadOnlyList<PedidoItemRequest> Itens);

public sealed record PedidoItemDto(
    int Id,
    int ProdutoId,
    string ProdutoCodigo,
    string ProdutoNome,
    int Quantidade,
    decimal PrecoUnitario,
    decimal Subtotal);

public sealed record PedidoDto(
    int Id,
    string Numero,
    int ClienteId,
    string ClienteNome,
    int VendedorId,
    DateTime DataPedido,
    DateOnly PrazoEntrega,
    string Status,
    string? Observacao,
    decimal ValorTotal,
    bool Atrasado,
    IReadOnlyList<PedidoItemDto> Itens);

public sealed record PedidoResumoDto(
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
    bool Atrasado)
{
    public static PedidoResumoDto De(PedidoResumo r, DateOnly hoje) => new(
        r.Id,
        r.Numero,
        r.ClienteId,
        r.ClienteNome,
        r.VendedorId,
        r.DataPedido,
        r.PrazoEntrega,
        r.Status.ToString(),
        r.ValorTotal,
        r.QuantidadeItens,
        r.EstaAtrasado(hoje));
}
