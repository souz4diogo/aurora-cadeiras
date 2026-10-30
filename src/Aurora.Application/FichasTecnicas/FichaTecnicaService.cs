using Aurora.Application.Abstracoes;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;

namespace Aurora.Application.FichasTecnicas;

public class FichaTecnicaService(
    IProdutoRepository produtos,
    IFichaTecnicaRepository fichas,
    IRoteiroRepository roteiros,
    IMateriaPrimaRepository materiasPrimas,
    IEstacaoRepository estacoes,
    IValidator<FichaTecnicaRequest> validador)
{
    public async Task<FichaTecnicaDto> ObterAsync(int produtoId, CancellationToken ct = default)
    {
        await GarantirProdutoAsync(produtoId, ct);
        return await MontarAsync(produtoId, ct);
    }

    /// <summary>Substitui a ficha técnica do produto (RF04). Cada material precisa ser consumido numa estação do roteiro.</summary>
    public async Task<FichaTecnicaDto> SubstituirAsync(int produtoId, FichaTecnicaRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);
        await GarantirProdutoAsync(produtoId, ct);

        var estacoesDoRoteiro = (await roteiros.ListarPorProdutoAsync(produtoId, ct)).Select(r => r.EstacaoId).ToHashSet();
        var foraDoRoteiro = request.Itens.Select(i => i.EstacaoId).FirstOrDefault(e => !estacoesDoRoteiro.Contains(e));
        if (foraDoRoteiro != 0)
        {
            throw new RegraDeNegocioException(
                "ESTACAO_FORA_DO_ROTEIRO",
                $"A estação {foraDoRoteiro} não faz parte do roteiro do produto. Ajuste o roteiro antes.",
                TipoErro.EntidadeNaoProcessavel);
        }

        var idsInformados = request.Itens.Select(i => i.MateriaPrimaId).Distinct().ToList();
        var existentes = (await materiasPrimas.ObterPorIdsAsync(idsInformados, ct)).Select(m => m.Id).ToHashSet();
        var inexistente = idsInformados.FirstOrDefault(id => !existentes.Contains(id));
        if (inexistente != 0)
        {
            throw new RegraDeNegocioException(
                "MATERIA_PRIMA_INEXISTENTE",
                $"A matéria-prima {inexistente} não existe.",
                TipoErro.EntidadeNaoProcessavel);
        }

        var itens = request.Itens
            .Select(i => new FichaTecnicaItem(produtoId, i.MateriaPrimaId, i.EstacaoId, i.Quantidade))
            .ToList();
        await fichas.SubstituirAsync(produtoId, itens, ct);
        return await MontarAsync(produtoId, ct);
    }

    private async Task GarantirProdutoAsync(int produtoId, CancellationToken ct)
    {
        if (await produtos.ObterPorIdAsync(produtoId, ct) is null)
        {
            throw new RegraDeNegocioException("PRODUTO_NAO_ENCONTRADO", "Produto não encontrado.", TipoErro.NaoEncontrado);
        }
    }

    private async Task<FichaTecnicaDto> MontarAsync(int produtoId, CancellationToken ct)
    {
        var itens = await fichas.ListarPorProdutoAsync(produtoId, ct);
        var materias = (await materiasPrimas.ObterPorIdsAsync(itens.Select(i => i.MateriaPrimaId).Distinct().ToList(), ct))
            .ToDictionary(m => m.Id);
        var nomesDasEstacoes = (await estacoes.ListarAsync(ct)).ToDictionary(e => e.Id, e => e.Nome);

        var linhas = itens
            .Select(i => (Item: i, Materia: materias[i.MateriaPrimaId]))
            .OrderBy(x => x.Item.EstacaoId).ThenBy(x => x.Materia.Codigo, StringComparer.Ordinal)
            .Select(x => new FichaTecnicaItemDto(
                x.Materia.Id,
                x.Materia.Codigo,
                x.Materia.Nome,
                x.Materia.Unidade.ParaCodigo(),
                x.Item.EstacaoId,
                nomesDasEstacoes.GetValueOrDefault(x.Item.EstacaoId, ""),
                x.Item.Quantidade))
            .ToList();
        return new FichaTecnicaDto(produtoId, linhas);
    }
}
