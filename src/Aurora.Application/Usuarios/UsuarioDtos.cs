using Aurora.Domain.Entidades;

namespace Aurora.Application.Usuarios;

public sealed record UsuarioDto(
    int Id,
    string Nome,
    string Email,
    byte PerfilId,
    string Perfil,
    int? ClienteId,
    bool Ativo,
    DateTime? UltimoLoginEm)
{
    public static UsuarioDto De(Usuario u) =>
        new(u.Id, u.Nome, u.Email, (byte)u.Perfil, u.Perfil.ToString(), u.ClienteId, u.Ativo, u.UltimoLoginEm);
}

public sealed record PerfilDto(byte Id, string Nome);

public sealed record CriarUsuarioRequest(string Nome, string Email, byte PerfilId, int? ClienteId);

public sealed record AtualizarUsuarioRequest(string Nome, string Email, byte PerfilId, int? ClienteId);

public sealed record AlterarAtivoRequest(bool Ativo);

/// <summary>A senha provisória só aparece nesta resposta; o banco guarda apenas o hash.</summary>
public sealed record UsuarioComSenhaProvisoriaDto(UsuarioDto Usuario, string SenhaProvisoria);
