using Aurora.Application.Abstracoes;
using Aurora.Domain.Entidades;
using Aurora.Domain.Excecoes;
using FluentValidation;

namespace Aurora.Application.Roteiros;

public class RoteiroService(
    IProdutoRepository produtos,
    IRoteiroRepository roteiros,
    IFichaTecnicaRepository fichas,
    IEstacaoRepository estacoes,
    IValidator<RoteiroRequest> validador)
{
    public async Task<RoteiroDto> ObterAsync(int produtoId, CancellationToken ct = default)
    {
        await GarantirProdutoAsync(produtoId, ct);
        return await MontarAsync(produtoId, ct);
    }

    /// <summary>Substitui o roteiro do produto (RF10): a ordem da lista vira a sequência das etapas da OP.</summary>
    public async Task<RoteiroDto> SubstituirAsync(int produtoId, RoteiroRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);
        await GarantirProdutoAsync(produtoId, ct);

        var cadastradas = (await estacoes.ListarAsync(ct)).ToDictionary(e => e.Id);
        byte? invalida = request.Estacoes
            .Where(id => !cadastradas.TryGetValue(id, out var estacao) || !estacao.GeraEtapa)
            .Select(id => (byte?)id)
            .FirstOrDefault();
        if (invalida is not null)
        {
            throw new RegraDeNegocioException(
                "ESTACAO_INVALIDA",
                $"A estação {invalida} não existe ou não gera etapa de produção.",
                TipoErro.EntidadeNaoProcessavel);
        }

        // A ficha técnica só consome material em estações do roteiro (docs/05 §3.2)
        var estacoesDaFicha = (await fichas.ListarPorProdutoAsync(produtoId, ct)).Select(i => i.EstacaoId).Distinct();
        var semRoteiro = estacoesDaFicha.FirstOrDefault(e => !request.Estacoes.Contains(e));
        if (semRoteiro != 0)
        {
            throw new RegraDeNegocioException(
                "ROTEIRO_INCOMPATIVEL_COM_FICHA",
                $"A ficha técnica consome material na estação {semRoteiro}; ajuste a ficha antes de retirá-la do roteiro.",
                TipoErro.EntidadeNaoProcessavel);
        }

        var etapas = request.Estacoes
            .Select((estacaoId, indice) => new RoteiroProduto(produtoId, estacaoId, (byte)(indice + 1)))
            .ToList();
        await roteiros.SubstituirAsync(produtoId, etapas, ct);
        return await MontarAsync(produtoId, ct);
    }

    private async Task GarantirProdutoAsync(int produtoId, CancellationToken ct)
    {
        if (await produtos.ObterPorIdAsync(produtoId, ct) is null)
        {
            throw new RegraDeNegocioException("PRODUTO_NAO_ENCONTRADO", "Produto não encontrado.", TipoErro.NaoEncontrado);
        }
    }

    private async Task<RoteiroDto> MontarAsync(int produtoId, CancellationToken ct)
    {
        var etapas = await roteiros.ListarPorProdutoAsync(produtoId, ct);
        var cadastradas = (await estacoes.ListarAsync(ct)).ToDictionary(e => e.Id);
        var linhas = etapas
            .OrderBy(e => e.Sequencia)
            .Select(e => new RoteiroEtapaDto(
                e.Sequencia,
                e.EstacaoId,
                cadastradas[e.EstacaoId].Nome,
                cadastradas[e.EstacaoId].GeraEtapa))
            .ToList();
        return new RoteiroDto(produtoId, linhas);
    }
}
