namespace Aurora.Domain.Entidades;

public class PedidoItem
{
    private PedidoItem()
    {
    }

    public PedidoItem(int produtoId, int quantidade, decimal precoUnitario)
    {
        ProdutoId = produtoId;
        Quantidade = quantidade;
        PrecoUnitario = precoUnitario;
    }

    public int Id { get; private set; }

    public int PedidoId { get; private set; }

    public int ProdutoId { get; private set; }

    public int Quantidade { get; private set; }

    /// <summary>Preço de venda no momento em que o item entrou no pedido (não muda se o produto for reprecificado).</summary>
    public decimal PrecoUnitario { get; private set; }

    public decimal Subtotal => Quantidade * PrecoUnitario;

    internal void Atualizar(int quantidade, decimal precoUnitario)
    {
        Quantidade = quantidade;
        PrecoUnitario = precoUnitario;
    }
}
