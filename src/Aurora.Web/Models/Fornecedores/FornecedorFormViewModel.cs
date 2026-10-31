using System.ComponentModel.DataAnnotations;
using Aurora.Web.Api.Fornecedores;

namespace Aurora.Web.Models.Fornecedores;

public sealed class FornecedorFormViewModel
{
    /// <summary>Preenchido na edição; nulo no cadastro.</summary>
    public int? Id { get; set; }

    [Required(ErrorMessage = "Informe a razão social.")]
    [StringLength(150, ErrorMessage = "A razão social aceita até 150 caracteres.")]
    [Display(Name = "Razão social")]
    public string RazaoSocial { get; set; } = "";

    [Required(ErrorMessage = "Informe o CNPJ.")]
    [Display(Name = "CNPJ")]
    public string Cnpj { get; set; } = "";

    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(150, ErrorMessage = "O e-mail aceita até 150 caracteres.")]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }

    [StringLength(20, ErrorMessage = "O telefone aceita até 20 caracteres.")]
    [Display(Name = "Telefone")]
    public string? Telefone { get; set; }

    [Display(Name = "Fornecedor de comunidade tradicional")]
    public bool ComunidadeTradicional { get; set; }

    public static FornecedorFormViewModel De(FornecedorResposta fornecedor) => new()
    {
        Id = fornecedor.Id,
        RazaoSocial = fornecedor.RazaoSocial,
        Cnpj = fornecedor.Cnpj,
        Email = fornecedor.Email,
        Telefone = fornecedor.Telefone,
        ComunidadeTradicional = fornecedor.ComunidadeTradicional,
    };

    public FornecedorRequisicao ParaRequisicao() => new(RazaoSocial, Cnpj, Email, Telefone, ComunidadeTradicional);
}
