namespace Aurora.Web.Models.Pedidos;

/// <summary>Produto na lista de escolha do pedido; o preço alimenta o cálculo do total na tela.</summary>
public sealed record ProdutoOpcao(int Id, string Rotulo, decimal Preco);
