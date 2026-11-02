namespace Aurora.Web.Seguranca;

/// <summary>
/// Nomes dos perfis (papéis) e grupos de perfis para [Authorize(Roles = ...)] e para o menu. Os nomes são os que a
/// API devolve no login; a API continua sendo a barreira real de permissão (docs/06 §3).
/// </summary>
public static class Perfis
{
    public const string Administrador = "Administrador";
    public const string GerenteProducao = "GerenteProducao";
    public const string Operador = "Operador";
    public const string Vendedor = "Vendedor";
    public const string Almoxarife = "Almoxarife";
    public const string Inspetor = "Inspetor";

    /// <summary>Administrador e Gerente de Produção (escrita em produtos).</summary>
    public const string AdministradorOuGerente = Administrador + "," + GerenteProducao;

    /// <summary>Administrador e Vendedor (escrita em clientes e pedidos).</summary>
    public const string AdministradorOuVendedor = Administrador + "," + Vendedor;

    /// <summary>Administrador e Almoxarife (escrita em fornecedores e matérias-primas).</summary>
    public const string AdministradorOuAlmoxarife = Administrador + "," + Almoxarife;

    /// <summary>Quem consulta clientes: Administrador, Gerente e Vendedor.</summary>
    public const string ConsultaDeClientes = Administrador + "," + GerenteProducao + "," + Vendedor;

    /// <summary>Quem consulta fornecedores: Administrador, Gerente e Almoxarife.</summary>
    public const string ConsultaDeFornecedores = Administrador + "," + GerenteProducao + "," + Almoxarife;

    /// <summary>Quem consulta produtos: todos os perfis internos.</summary>
    public const string ConsultaDeProdutos =
        Administrador + "," + GerenteProducao + "," + Operador + "," + Vendedor + "," + Almoxarife + "," + Inspetor;

    /// <summary>Quem consulta matérias-primas: Administrador, Gerente, Operador e Almoxarife.</summary>
    public const string ConsultaDeMateriasPrimas = Administrador + "," + GerenteProducao + "," + Operador + "," + Almoxarife;

    /// <summary>Quem consulta pedidos: Administrador, Gerente, Vendedor (só os próprios) e Almoxarife.</summary>
    public const string ConsultaDePedidos = Administrador + "," + GerenteProducao + "," + Vendedor + "," + Almoxarife;

    /// <summary>Quem consulta OPs e o quadro de produção: Administrador, Gerente, Vendedor (só as dos próprios pedidos) e Inspetor.</summary>
    public const string ConsultaDeProducao = Administrador + "," + GerenteProducao + "," + Vendedor + "," + Inspetor;

    /// <summary>Quem consulta o estoque (saldos, movimentos e alertas): Administrador, Gerente e Almoxarife.</summary>
    public const string ConsultaDeEstoque = Administrador + "," + GerenteProducao + "," + Almoxarife;

    /// <summary>Quem consulta pedidos de compra: Administrador, Gerente e Almoxarife (que também cria, emite, cancela e recebe).</summary>
    public const string ConsultaDeCompras = Administrador + "," + GerenteProducao + "," + Almoxarife;

    /// <summary>Quem vê a fila e o histórico de expedição: Administrador, Gerente, Almoxarife (que expede) e Vendedor (só os próprios pedidos).</summary>
    public const string ConsultaDeExpedicoes = Administrador + "," + GerenteProducao + "," + Almoxarife + "," + Vendedor;

    /// <summary>Quem gera relatórios: Administrador, Gerente, Vendedor e Almoxarife (a API limita os tipos por perfil).</summary>
    public const string ConsultaDeRelatorios = Administrador + "," + GerenteProducao + "," + Vendedor + "," + Almoxarife;

    /// <summary>Quem consulta as inspeções de uma OP: Administrador, Gerente e Inspetor.</summary>
    public const string ConsultaDeInspecoes = Administrador + "," + GerenteProducao + "," + Inspetor;
}
