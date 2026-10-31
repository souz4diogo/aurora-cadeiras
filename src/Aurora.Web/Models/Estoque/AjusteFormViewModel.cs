using System.ComponentModel.DataAnnotations;
using Aurora.Web.Api.Estoque;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Models.Estoque;

public sealed class AjusteFormViewModel
{
    [Required(ErrorMessage = "Escolha a matéria-prima.")]
    [Display(Name = "Matéria-prima")]
    public int? MateriaPrimaId { get; set; }

    [Required(ErrorMessage = "Informe o saldo contado.")]
    [Range(0, 999999999, ErrorMessage = "O saldo contado não pode ser negativo.")]
    [Display(Name = "Saldo contado")]
    public decimal? NovaQuantidade { get; set; }

    [Required(ErrorMessage = "Informe o motivo do ajuste.")]
    [StringLength(200, ErrorMessage = "O motivo aceita até 200 caracteres.")]
    [Display(Name = "Motivo")]
    public string Motivo { get; set; } = "";

    /// <summary>Matérias-primas ativas com o saldo no texto; preenchida pelo controller a cada exibição.</summary>
    public IReadOnlyList<SelectListItem> MateriasPrimas { get; set; } = [];

    public AjusteDeEstoqueRequisicao ParaRequisicao() => new(MateriaPrimaId!.Value, NovaQuantidade!.Value, Motivo);
}
