using Aurora.Domain.Enums;

namespace Aurora.Api.Seguranca;

/// <summary>Nomes dos perfis para [Authorize(Roles = ...)]; derivados de PerfilUsuario para nunca divergirem.</summary>
public static class Perfis
{
    public const string Administrador = nameof(PerfilUsuario.Administrador);
    public const string GerenteProducao = nameof(PerfilUsuario.GerenteProducao);
    public const string Operador = nameof(PerfilUsuario.Operador);
    public const string Vendedor = nameof(PerfilUsuario.Vendedor);
    public const string Almoxarife = nameof(PerfilUsuario.Almoxarife);
    public const string Inspetor = nameof(PerfilUsuario.Inspetor);
    public const string Cliente = nameof(PerfilUsuario.Cliente);
}
