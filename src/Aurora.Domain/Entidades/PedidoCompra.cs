using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;

namespace Aurora.Domain.Entidades;

/// <summary>
/// Pedido de compra de matéria-prima a um fornecedor (RF16). Nasce Solicitado, é Emitido ao fornecedor e vai sendo
/// Recebido por item (RF17). Quem muda o status e a quantidade recebida é o banco (emitir/cancelar de forma atômica e
/// sp_ReceberItemCompra); aqui ficam as regras que dizem o que pode ser feito em cada situação.
/// </summary>
public class PedidoCompra
{
    private readonly List<PedidoCompraItem> _itens = [];

    private PedidoCompra()
    {
    }

    public PedidoCompra(int fornecedorId, int compradorId, DateOnly? dataPrevista, IEnumerable<ItemDePedidoCompra> itens)
    {
        FornecedorId = fornecedorId;
        CompradorId = compradorId;
        DataPrevista = dataPrevista;
        Status = StatusPedidoCompra.Solicitado;

        foreach (var item in itens)
        {
            if (_itens.Exists(i => i.MateriaPrimaId == item.MateriaPrimaId))
            {
                throw new RegraDeNegocioException(
                    "MATERIA_PRIMA_REPETIDA", "A mesma matéria-prima não pode aparecer duas vezes; some as quantidades.", TipoErro.Validacao);
            }

            _itens.Add(new PedidoCompraItem(item.MateriaPrimaId, item.Quantidade, item.CustoUnitario));
        }

        if (_itens.Count == 0)
        {
            throw new RegraDeNegocioException(
                "PEDIDO_COMPRA_SEM_ITENS", "O pedido de compra precisa de ao menos um item.", TipoErro.Validacao);
        }
    }

    public int Id { get; private set; }

    /// <summary>Gerado pelo banco a partir de uma sequência (PC-2026-00001).</summary>
    public string Numero { get; private set; } = null!;

    public int FornecedorId { get; private set; }

    /// <summary>Quem registrou o pedido (o almoxarife logado).</summary>
    public int CompradorId { get; private set; }

    public StatusPedidoCompra Status { get; private set; }

    public DateOnly? DataPrevista { get; private set; }

    /// <summary>Preenchida pelo banco (UTC).</summary>
    public DateTime CriadoEm { get; private set; }

    public IReadOnlyCollection<PedidoCompraItem> Itens => _itens;

    public decimal ValorTotal => _itens.Sum(i => i.Subtotal);

    /// <summary>Só uma solicitação ainda não enviada ao fornecedor pode ser emitida.</summary>
    public bool PodeEmitir => Status == StatusPedidoCompra.Solicitado;

    /// <summary>
    /// Cancela-se antes de chegar qualquer coisa: depois do primeiro recebimento o material já entrou no estoque
    /// (o livro-razão é imutável) e o pedido segue até ser recebido.
    /// </summary>
    public bool PodeCancelar =>
        Status is StatusPedidoCompra.Solicitado or StatusPedidoCompra.Emitido && _itens.TrueForAll(i => i.QuantidadeRecebida == 0);

    public bool PodeReceber => Status is StatusPedidoCompra.Emitido or StatusPedidoCompra.ParcialmenteRecebido;

    /// <summary>Emitido (ou parcial) com a data prevista vencida e ainda faltando material.</summary>
    public bool EstaAtrasado(DateOnly hoje) => PodeReceber && DataPrevista < hoje;
}
