using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Models.Produtos;

/// <summary>Linhas de produto (ids da tabela LinhaProduto) com o nome para a lista de escolha.</summary>
public static class LinhasDeProduto
{
    private static readonly (byte Id, string Nome)[] Opcoes =
    [
        (1, "Escritório"),
        (2, "Jantar"),
        (3, "Estofada"),
    ];

    public static IReadOnlyList<SelectListItem> ParaLista() =>
        Opcoes.Select(o => new SelectListItem(o.Nome, o.Id.ToString(CultureInfo.InvariantCulture))).ToList();
}
