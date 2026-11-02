using System.ComponentModel.DataAnnotations;
using Aurora.Web.Api.Compras;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Models.Compras;

public sealed class PedidoCompraFormViewModel
{
    [Required(ErrorMessage = "Escolha o fornecedor.")]
    [Display(Name = "Fornecedor")]
    public int? FornecedorId { get; set; }

    [DataType(DataType.Date)]
    [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}", ApplyFormatInEditMode = true)]
    [Display(Name = "Entrega prevista")]
    public DateOnly? DataPrevista { get; set; }

    public List<CompraItemFormViewModel> Itens { get; set; } = [];

    /// <summary>Fornecedores ativos para escolher; preenchida pelo controller a cada exibição.</summary>
    public IReadOnlyList<SelectListItem> Fornecedores { get; set; } = [];

    /// <summary>Matérias-primas ativas (com o custo atual no texto); preenchida pelo controller.</summary>
    public IReadOnlyList<SelectListItem> MateriasPrimas { get; set; } = [];

    public PedidoCompraRequisicao ParaRequisicao() => new(
        FornecedorId!.Value,
        DataPrevista,
        Itens.Select(i => new PedidoCompraItemRequisicao(i.MateriaPrimaId!.Value, i.Quantidade!.Value, i.CustoUnitario)).ToList());
}

public sealed class CompraItemFormViewModel
{
    [Required(ErrorMessage = "Escolha a matéria-prima.")]
    [Display(Name = "Matéria-prima")]
    public int? MateriaPrimaId { get; set; }

    [Required(ErrorMessage = "Informe a quantidade.")]
    [Range(0.001, 999999999, ErrorMessage = "A quantidade deve ser maior que zero.")]
    [Display(Name = "Quantidade")]
    public decimal? Quantidade { get; set; }

    [Range(0, 999999999, ErrorMessage = "O custo não pode ser negativo.")]
    [Display(Name = "Custo unitário")]
    public decimal? CustoUnitario { get; set; }
}
