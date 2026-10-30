using System.ComponentModel.DataAnnotations;
using Aurora.Web.Api.Clientes;

namespace Aurora.Web.Models.Clientes;

public sealed class ClienteFormViewModel
{
    /// <summary>Preenchido na edição; nulo no cadastro.</summary>
    public int? Id { get; set; }

    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(150, ErrorMessage = "O nome aceita até 150 caracteres.")]
    [Display(Name = "Nome")]
    public string Nome { get; set; } = "";

    [Required(ErrorMessage = "Informe o CPF ou o CNPJ.")]
    [Display(Name = "CPF ou CNPJ")]
    public string CpfCnpj { get; set; } = "";

    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(150, ErrorMessage = "O e-mail aceita até 150 caracteres.")]
    [Display(Name = "E-mail")]
    public string? Email { get; set; }

    [StringLength(20, ErrorMessage = "O telefone aceita até 20 caracteres.")]
    [Display(Name = "Telefone")]
    public string? Telefone { get; set; }

    [StringLength(80, ErrorMessage = "A cidade aceita até 80 caracteres.")]
    [Display(Name = "Cidade")]
    public string? Cidade { get; set; }

    [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "A UF deve ter 2 letras.")]
    [Display(Name = "UF")]
    public string? Uf { get; set; }

    public static ClienteFormViewModel De(ClienteResposta cliente) => new()
    {
        Id = cliente.Id,
        Nome = cliente.Nome,
        CpfCnpj = cliente.CpfCnpj,
        Email = cliente.Email,
        Telefone = cliente.Telefone,
        Cidade = cliente.Cidade,
        Uf = cliente.Uf,
    };

    public ClienteRequisicao ParaRequisicao() => new(Nome, CpfCnpj, Email, Telefone, Cidade, Uf);
}
