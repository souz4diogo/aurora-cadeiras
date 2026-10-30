using System.Security.Claims;
using Aurora.Web.Models.Navegacao;

namespace Aurora.Web.Seguranca;

/// <summary>Menu da aplicação por perfil, conforme o mapa de navegação do docs/07 §2 (somente telas já existentes).</summary>
public static class MenuPrincipal
{
    private static readonly ItemDeMenu[] Itens =
    [
        new("Clientes", "Cadastro de clientes", "Clientes", Perfis.AdministradorOuVendedor),
        new("Fornecedores", "Fornecedores e comunidades tradicionais", "Fornecedores", Perfis.AdministradorOuAlmoxarife),
        new("Produtos", "Produtos, ficha técnica e roteiro", "Produtos", Perfis.AdministradorOuGerente),
        new("Matérias-primas", "Materiais, saldo e estoque mínimo", "MateriasPrimas", Perfis.AdministradorOuAlmoxarife),
        new("Pedidos", "Pedidos de venda", "Pedidos", Perfis.Administrador + "," + Perfis.GerenteProducao + "," + Perfis.Vendedor),
    ];

    public static IReadOnlyList<ItemDeMenu> Para(ClaimsPrincipal usuario) =>
        Itens.Where(item => item.Perfis.Split(',').Any(usuario.IsInRole)).ToList();
}
