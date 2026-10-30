using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;

namespace Aurora.Domain.Entidades;

/// <summary>Pedido de venda. Só pode ser editado enquanto estiver Aberto; depois disso o fluxo é por procedures.</summary>
public class Pedido
{
    private readonly List<PedidoItem> _itens = [];

    private Pedido()
    {
    }

    public Pedido(int clienteId, int vendedorId, DateOnly prazoEntrega, string? observacao, IEnumerable<ItemDePedido> itens)
    {
        ClienteId = clienteId;
        VendedorId = vendedorId;
        PrazoEntrega = prazoEntrega;
        Observacao = observacao;
        Status = StatusPedido.Aberto;
        SubstituirItens(itens);
    }

    public int Id { get; private set; }

    /// <summary>Gerado pelo banco a partir de uma sequência (PV-2026-00001).</summary>
    public string Numero { get; private set; } = null!;

    public int ClienteId { get; private set; }

    public int VendedorId { get; private set; }

    /// <summary>Preenchida pelo banco (UTC) na criação.</summary>
    public DateTime DataPedido { get; private set; }

    public DateOnly PrazoEntrega { get; private set; }

    public StatusPedido Status { get; private set; }

    public string? Observacao { get; private set; }

    public decimal ValorTotal { get; private set; }

    public IReadOnlyCollection<PedidoItem> Itens => _itens;

    public bool EstaAtrasado(DateOnly hoje) =>
        Status is StatusPedido.Confirmado or StatusPedido.EmProducao or StatusPedido.Pronto && PrazoEntrega < hoje;

    public void AtualizarDados(int clienteId, DateOnly prazoEntrega, string? observacao)
    {
        GarantirAberto();
        ClienteId = clienteId;
        PrazoEntrega = prazoEntrega;
        Observacao = observacao;
    }

    /// <summary>
    /// Troca os itens pelo conjunto informado: mantém o item do mesmo produto (atualizando a quantidade e o preço),
    /// remove os que saíram e inclui os novos. Recalcula o valor total.
    /// </summary>
    public void SubstituirItens(IEnumerable<ItemDePedido> novos)
    {
        GarantirAberto();
        var desejados = novos.ToList();
        if (desejados.Count == 0)
        {
            throw new RegraDeNegocioException("PEDIDO_SEM_ITENS", "O pedido precisa de ao menos um item.", TipoErro.Validacao);
        }

        _itens.RemoveAll(existente => desejados.All(d => d.ProdutoId != existente.ProdutoId));
        foreach (var desejado in desejados)
        {
            var existente = _itens.Find(i => i.ProdutoId == desejado.ProdutoId);
            if (existente is null)
            {
                _itens.Add(new PedidoItem(desejado.ProdutoId, desejado.Quantidade, desejado.PrecoUnitario));
            }
            else
            {
                existente.Atualizar(desejado.Quantidade, desejado.PrecoUnitario);
            }
        }

        ValorTotal = _itens.Sum(i => i.Subtotal);
    }

    private void GarantirAberto()
    {
        if (Status != StatusPedido.Aberto)
        {
            throw new RegraDeNegocioException(
                "STATUS_INVALIDO", "Só é possível editar um pedido que ainda está Aberto.");
        }
    }
}
