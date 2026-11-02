namespace Aurora.Domain.Entidades;

/// <summary>Matéria-prima pedida ao fornecedor. A quantidade recebida só muda por sp_ReceberItemCompra.</summary>
public class PedidoCompraItem
{
    private PedidoCompraItem()
    {
    }

    public PedidoCompraItem(int materiaPrimaId, decimal quantidade, decimal custoUnitario)
    {
        MateriaPrimaId = materiaPrimaId;
        Quantidade = quantidade;
        CustoUnitario = custoUnitario;
    }

    public int Id { get; private set; }

    public int PedidoCompraId { get; private set; }

    public int MateriaPrimaId { get; private set; }

    public decimal Quantidade { get; private set; }

    /// <summary>Custo combinado com o fornecedor neste pedido (vira o custo atual da matéria-prima no recebimento).</summary>
    public decimal CustoUnitario { get; private set; }

    public decimal QuantidadeRecebida { get; private set; }

    /// <summary>O que ainda falta chegar.</summary>
    public decimal Saldo => Quantidade - QuantidadeRecebida;

    public decimal Subtotal => Quantidade * CustoUnitario;
}
