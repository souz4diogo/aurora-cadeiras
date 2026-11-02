namespace Aurora.Domain.Entidades;

/// <summary>Item desejado para o pedido de compra: o custo já vem definido por quem monta o pedido.</summary>
public readonly record struct ItemDePedidoCompra(int MateriaPrimaId, decimal Quantidade, decimal CustoUnitario);
