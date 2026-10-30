using System.ComponentModel.DataAnnotations;

namespace Aurora.Web.Models.Produtos;

public sealed class FichaTecnicaLinhaViewModel
{
    [Required(ErrorMessage = "Escolha a matéria-prima.")]
    [Display(Name = "Matéria-prima")]
    public int? MateriaPrimaId { get; set; }

    [Required(ErrorMessage = "Escolha a estação.")]
    [Display(Name = "Estação")]
    public byte? EstacaoId { get; set; }

    [Range(typeof(decimal), "0,001", "999999999", ErrorMessage = "A quantidade deve ser maior que zero.")]
    [Display(Name = "Quantidade")]
    public decimal Quantidade { get; set; }
}
