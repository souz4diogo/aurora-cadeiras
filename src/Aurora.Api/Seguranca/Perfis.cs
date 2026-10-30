using Aurora.Domain.Enums;

namespace Aurora.Api.Seguranca;

/// <summary>
/// Nomes dos perfis para [Authorize(Roles = ...)], derivados de PerfilUsuario para nunca divergirem,
/// e grupos de perfis (lista separada por vírgula) conforme a matriz de permissões do docs/06 §3.
/// </summary>
public static class Perfis
{
    public const string Administrador = nameof(PerfilUsuario.Administrador);
    public const string GerenteProducao = nameof(PerfilUsuario.GerenteProducao);
    public const string Operador = nameof(PerfilUsuario.Operador);
    public const string Vendedor = nameof(PerfilUsuario.Vendedor);
    public const string Almoxarife = nameof(PerfilUsuario.Almoxarife);
    public const string Inspetor = nameof(PerfilUsuario.Inspetor);
    public const string Cliente = nameof(PerfilUsuario.Cliente);

    /// <summary>Todos os perfis internos da fábrica (todos, exceto o Cliente).</summary>
    public const string Internos =
        Administrador + "," + GerenteProducao + "," + Operador + "," + Vendedor + "," + Almoxarife + "," + Inspetor;

    /// <summary>Administrador e Gerente de Produção (escrita em produtos).</summary>
    public const string AdministradorOuGerente = Administrador + "," + GerenteProducao;

    /// <summary>Administrador e Almoxarife (escrita em matérias-primas e fornecedores).</summary>
    public const string AdministradorOuAlmoxarife = Administrador + "," + Almoxarife;

    /// <summary>Quem consulta matérias-primas: Administrador, Gerente, Operador e Almoxarife.</summary>
    public const string ConsultaDeMateriaPrima = Administrador + "," + GerenteProducao + "," + Operador + "," + Almoxarife;
}
