using System.ComponentModel.DataAnnotations;

namespace Aurora.Web.Models.Produtos;

public sealed class RoteiroLinhaViewModel
{
    public byte EstacaoId { get; set; }

    public string Nome { get; set; } = "";

    [Display(Name = "Faz parte do roteiro")]
    public bool Incluir { get; set; }

    [Range(1, 99, ErrorMessage = "A posição deve ficar entre 1 e 99.")]
    [Display(Name = "Posição")]
    public int? Posicao { get; set; }
}
