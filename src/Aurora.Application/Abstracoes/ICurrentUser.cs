using Aurora.Domain.Enums;

namespace Aurora.Application.Abstracoes;

public interface ICurrentUser
{
    int? UsuarioId { get; }

    /// <summary>Perfil do usuário autenticado, ou nulo se não houver usuário ou o perfil for desconhecido.</summary>
    PerfilUsuario? Perfil { get; }
}
