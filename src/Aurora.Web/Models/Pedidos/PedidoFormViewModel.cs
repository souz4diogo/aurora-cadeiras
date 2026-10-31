using System.ComponentModel.DataAnnotations;
using Aurora.Web.Api.Pedidos;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Models.Pedidos;

public sealed class PedidoFormViewModel
{
    /// <summary>Preenchido na edição; nulo no pedido novo.</summary>
    public int? Id { get; set; }

    /// <summary>Número gerado pelo sistema, só para exibir na edição.</summary>
    public string? Numero { get; set; }

    [Required(ErrorMessage = "Escolha o cliente.")]
    [Display(Name = "Cliente")]
    public int? ClienteId { get; set; }

    [Required(ErrorMessage = "Informe o prazo de entrega.")]
    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Prazo de entrega")]
    public DateOnly? PrazoEntrega { get; set; }

    [StringLength(300, ErrorMessage = "A observação aceita até 300 caracteres.")]
    [Display(Name = "Observação")]
    public string? Observacao { get; set; }

    public List<PedidoItemFormViewModel> Itens { get; set; } = [];

    /// <summary>Clientes ativos para escolher; preenchida pelo controller a cada exibição do formulário.</summary>
    public IReadOnlyList<SelectListItem> Clientes { get; set; } = [];

    /// <summary>Produtos para escolher, com o preço para o total em tela; preenchida pelo controller.</summary>
    public IReadOnlyList<ProdutoOpcao> Produtos { get; set; } = [];

    public static PedidoFormViewModel De(PedidoResposta pedido) => new()
    {
        Id = pedido.Id,
        Numero = pedido.Numero,
        ClienteId = pedido.ClienteId,
        PrazoEntrega = pedido.PrazoEntrega,
        Observacao = pedido.Observacao,
        Itens = pedido.Itens.Select(i => new PedidoItemFormViewModel { ProdutoId = i.ProdutoId, Quantidade = i.Quantidade }).ToList(),
    };

    public PedidoRequisicao ParaRequisicao() =>
        new(ClienteId!.Value, PrazoEntrega!.Value, Observacao, Itens.Select(i => new PedidoItemRequisicao(i.ProdutoId!.Value, i.Quantidade)).ToList());
}
