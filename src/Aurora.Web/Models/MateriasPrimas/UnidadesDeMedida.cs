using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Models.MateriasPrimas;

/// <summary>Unidades de medida aceitas pela API (código) com o nome por extenso para a lista de escolha.</summary>
public static class UnidadesDeMedida
{
    private static readonly (string Codigo, string Nome)[] Opcoes =
    [
        ("un", "Unidade (un)"),
        ("m", "Metro (m)"),
        ("m2", "Metro quadrado (m²)"),
        ("m3", "Metro cúbico (m³)"),
        ("l", "Litro (l)"),
    ];

    public static IReadOnlyList<SelectListItem> ParaLista() =>
        Opcoes.Select(o => new SelectListItem(o.Nome, o.Codigo)).ToList();
}
