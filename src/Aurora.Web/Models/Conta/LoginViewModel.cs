using System.ComponentModel.DataAnnotations;

namespace Aurora.Web.Models.Conta;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Informe a senha.")]
    [DataType(DataType.Password)]
    [Display(Name = "Senha")]
    public string Senha { get; set; } = "";

    /// <summary>Preenchido quando a sessão expirou, para a tela avisar o motivo de pedir o login de novo.</summary>
    public bool SessaoExpirada { get; set; }
}
