using Aurora.Application.Abstracoes;
using Aurora.Application.Comum;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;

namespace Aurora.Application.Qualidade;

/// <summary>Inspeção de qualidade (RF18). A decisão roda em procedure; este serviço valida a entrada e devolve o resultado.</summary>
public class InspecaoService(IQualidadeRepository qualidade, IValidator<InspecaoRequest> validador)
{
    /// <summary>
    /// Registra a inspeção da OP. Aprovada: conclui a OP, dá entrada do produto acabado e, se foi a última OP, deixa o
    /// pedido Pronto. Reprovada: reabre as etapas a partir da estação de retrabalho (RN06).
    /// </summary>
    public async Task<InspecaoDto> RegistrarAsync(int ordemProducaoId, InspecaoRequest request, CancellationToken ct = default)
    {
        await validador.ValidateAndThrowAsync(request, ct);
        EnumPorNome.TentarObter(request.Resultado, out ResultadoInspecao resultado);

        // A estação de retrabalho só faz sentido na reprovação; na aprovação ela é descartada
        var estacao = resultado == ResultadoInspecao.Reprovada ? request.EstacaoRetrabalhoId : null;
        await qualidade.RegistrarInspecaoAsync(ordemProducaoId, resultado, estacao, Texto.OuNulo(request.Observacao), ct);

        var registrada = await qualidade.ObterUltimaDaOrdemAsync(ordemProducaoId, ct)
            ?? throw new RegraDeNegocioException(
                "ORDEM_PRODUCAO_NAO_ENCONTRADA", "Ordem de produção não encontrada.", TipoErro.NaoEncontrado);
        return InspecaoDto.De(registrada);
    }

    public async Task<Pagina<InspecaoDto>> ListarAsync(
        int? ordemProducaoId,
        ResultadoInspecao? resultado,
        DateOnly? de,
        DateOnly? ate,
        int pagina,
        int tamanho,
        CancellationToken ct = default)
    {
        (pagina, tamanho) = Paginacao.Normalizar(pagina, tamanho);
        var filtro = new FiltroDeInspecoes(ordemProducaoId, resultado, de, ate);
        var (itens, total) = await qualidade.ListarAsync(filtro, pagina, tamanho, ct);
        return new Pagina<InspecaoDto>(itens.Select(InspecaoDto.De).ToList(), total, pagina, tamanho);
    }
}
