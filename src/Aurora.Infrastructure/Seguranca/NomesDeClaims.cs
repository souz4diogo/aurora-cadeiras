using Microsoft.IdentityModel.JsonWebTokens;

namespace Aurora.Infrastructure.Seguranca;

/// <summary>Nomes das claims do token de acesso, compartilhados por quem emite e por quem lê o JWT.</summary>
public static class NomesDeClaims
{
    public const string UsuarioId = JwtRegisteredClaimNames.Sub;
    public const string Nome = JwtRegisteredClaimNames.Name;
    public const string Perfil = "role";
    public const string ClienteId = "clienteId";
}
