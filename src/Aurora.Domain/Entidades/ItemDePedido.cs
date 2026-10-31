namespace Aurora.Domain.Entidades;

/// <summary>Item desejado para o pedido: o preço já vem definido por quem monta o pedido.</summary>
public readonly record struct ItemDePedido(int ProdutoId, int Quantidade, decimal PrecoUnitario);
