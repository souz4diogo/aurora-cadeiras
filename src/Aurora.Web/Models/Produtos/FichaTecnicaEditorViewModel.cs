using Aurora.Web.Api.Produtos;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Models.Produtos;

public sealed class FichaTecnicaEditorViewModel
{
    public int ProdutoId { get; set; }

    public string ProdutoNome { get; set; } = "";

    public List<FichaTecnicaLinhaViewModel> Itens { get; set; } = [];

    /// <summary>Matérias-primas ativas para escolher; preenchida pelo controller a cada exibição.</summary>
    public IReadOnlyList<SelectListItem> Materias { get; set; } = [];

    /// <summary>Estações do roteiro do produto: a ficha só consome material nelas.</summary>
    public IReadOnlyList<SelectListItem> Estacoes { get; set; } = [];

    public static FichaTecnicaEditorViewModel De(ProdutoResposta produto, FichaTecnicaResposta ficha) => new()
    {
        ProdutoId = produto.Id,
        ProdutoNome = produto.Nome,
        Itens = ficha.Itens
            .Select(i => new FichaTecnicaLinhaViewModel { MateriaPrimaId = i.MateriaPrimaId, EstacaoId = i.EstacaoId, Quantidade = i.Quantidade })
            .ToList(),
    };

    public FichaTecnicaRequisicao ParaRequisicao() =>
        new(Itens.Select(i => new FichaTecnicaItemRequisicao(i.MateriaPrimaId!.Value, i.EstacaoId!.Value, i.Quantidade)).ToList());
}
