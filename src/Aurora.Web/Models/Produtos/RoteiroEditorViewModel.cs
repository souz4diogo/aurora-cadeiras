using Aurora.Web.Api.Estacoes;
using Aurora.Web.Api.Produtos;

namespace Aurora.Web.Models.Produtos;

public sealed class RoteiroEditorViewModel
{
    public int ProdutoId { get; set; }

    public string ProdutoNome { get; set; } = "";

    /// <summary>Uma linha por estação que gera etapa; as marcadas, na ordem da posição, formam o roteiro.</summary>
    public List<RoteiroLinhaViewModel> Estacoes { get; set; } = [];

    public static RoteiroEditorViewModel De(ProdutoResposta produto, RoteiroResposta roteiro, IEnumerable<EstacaoResposta> estacoes)
    {
        var sequenciaPorEstacao = roteiro.Etapas.ToDictionary(e => e.EstacaoId, e => (int)e.Sequencia);
        return new RoteiroEditorViewModel
        {
            ProdutoId = produto.Id,
            ProdutoNome = produto.Nome,
            Estacoes = estacoes
                .Where(e => e.GeraEtapa)
                .Select(e => new RoteiroLinhaViewModel
                {
                    EstacaoId = e.Id,
                    Nome = e.Nome,
                    Incluir = sequenciaPorEstacao.ContainsKey(e.Id),
                    Posicao = sequenciaPorEstacao.TryGetValue(e.Id, out var sequencia) ? sequencia : null,
                })
                .ToList(),
        };
    }

    /// <summary>Ids das estações marcadas, pela posição informada (sem posição, ao final, na ordem da fábrica).</summary>
    public RoteiroRequisicao ParaRequisicao() =>
        new(Estacoes
            .Select((linha, ordem) => (Linha: linha, Ordem: ordem))
            .Where(x => x.Linha.Incluir)
            .OrderBy(x => x.Linha.Posicao ?? int.MaxValue)
            .ThenBy(x => x.Ordem)
            .Select(x => x.Linha.EstacaoId)
            .ToList());
}
