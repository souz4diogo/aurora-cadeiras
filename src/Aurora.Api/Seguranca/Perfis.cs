namespace Aurora.Api.Seguranca;

/// <summary>Nomes dos perfis usados em [Authorize(Roles = ...)] (iguais aos de PerfilUsuario).</summary>
public static class Perfis
{
    public const string Administrador = "Administrador";
    public const string GerenteProducao = "GerenteProducao";
    public const string Operador = "Operador";
    public const string Vendedor = "Vendedor";
    public const string Almoxarife = "Almoxarife";
    public const string Inspetor = "Inspetor";
    public const string Cliente = "Cliente";
}
