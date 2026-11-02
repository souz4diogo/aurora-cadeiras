using System.ComponentModel.DataAnnotations;
using Aurora.Web.Api.Comum;
using Aurora.Web.Api.Usuarios;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Models.Usuarios;

public sealed record UsuariosListaViewModel(Pagina<UsuarioResposta> Pagina, string? Busca);

/// <summary>Tela única de resultado de criação e de redefinição: mostra a senha provisória uma vez só.</summary>
public sealed record SenhaProvisoriaViewModel(string Nome, string Email, string Senha, bool Redefinicao);

public sealed class UsuarioFormViewModel
{
    /// <summary>Código do perfil Cliente na tabela Perfil; esse perfil exige um cliente vinculado.</summary>
    public const byte PerfilCliente = 7;

    public int? Id { get; set; }

    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(120, ErrorMessage = "O nome aceita até 120 caracteres.")]
    [Display(Name = "Nome")]
    public string Nome { get; set; } = "";

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(150, ErrorMessage = "O e-mail aceita até 150 caracteres.")]
    [Display(Name = "E-mail")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Escolha o perfil.")]
    [Display(Name = "Perfil")]
    public byte? PerfilId { get; set; }

    [Display(Name = "Cliente vinculado")]
    public int? ClienteId { get; set; }

    public IReadOnlyList<SelectListItem> Perfis { get; set; } = [];

    public IReadOnlyList<SelectListItem> Clientes { get; set; } = [];

    public static UsuarioFormViewModel De(UsuarioResposta usuario) => new()
    {
        Id = usuario.Id,
        Nome = usuario.Nome,
        Email = usuario.Email,
        PerfilId = usuario.PerfilId,
        ClienteId = usuario.ClienteId,
    };

    public UsuarioRequisicao ParaRequisicao() =>
        new(Nome, Email, PerfilId!.Value, PerfilId == PerfilCliente ? ClienteId : null);
}
