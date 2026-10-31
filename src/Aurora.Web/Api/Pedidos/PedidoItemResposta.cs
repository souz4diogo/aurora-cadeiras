namespace Aurora.Web.Api.Pedidos;

public sealed record PedidoItemResposta(
    int Id,
    int ProdutoId,
    string ProdutoCodigo,
    string ProdutoNome,
    int Quantidade,
    decimal PrecoUnitario,
    decimal Subtotal);
