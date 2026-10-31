using Aurora.Application.Abstracoes;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;

namespace Aurora.Application.Producao;

/// <summary>Início, fim e perdas das etapas. Início e fim rodam em procedures; a perda é um registro simples.</summary>
public class EtapaService(
    IProducaoRepository producao,
    IUnitOfWork unitOfWork,
    ICurrentUser usuarioAtual,
    IValidator<ApontamentoRequest> validadorDeApontamento,
    IValidator<PerdaRequest> validadorDePerda)
{
    /// <summary>
    /// Inicia a etapa (RN05) e baixa do estoque a matéria-prima da ficha técnica daquela estação (RN03).
    /// Sem saldo suficiente a etapa não inicia (RN04). Reenviar o mesmo <c>ClientRequestId</c> devolve o estado
    /// atual sem baixar o estoque outra vez.
    /// </summary>
    public async Task<EtapaDto> IniciarAsync(int etapaId, ApontamentoRequest request, CancellationToken ct = default)
    {
        await validadorDeApontamento.ValidateAndThrowAsync(request, ct);
        await ObterOuFalharAsync(etapaId, ct);

        await producao.IniciarEtapaAsync(etapaId, request.ClientRequestId, request.DataHoraCliente.UtcDateTime, ct);
        return EtapaDto.De(await ObterOuFalharAsync(etapaId, ct));
    }

    /// <summary>Finaliza a etapa em andamento. A etapa de Qualidade só termina por inspeção.</summary>
    public async Task<EtapaDto> FinalizarAsync(int etapaId, ApontamentoRequest request, CancellationToken ct = default)
    {
        await validadorDeApontamento.ValidateAndThrowAsync(request, ct);
        await ObterOuFalharAsync(etapaId, ct);

        await producao.FinalizarEtapaAsync(etapaId, request.ClientRequestId, request.DataHoraCliente.UtcDateTime, ct);
        return EtapaDto.De(await ObterOuFalharAsync(etapaId, ct));
    }

    /// <summary>Registra refugo ou retrabalho numa etapa em andamento (RF12).</summary>
    public async Task<PerdaDto> RegistrarPerdaAsync(int etapaId, PerdaRequest request, CancellationToken ct = default)
    {
        await validadorDePerda.ValidateAndThrowAsync(request, ct);
        var usuarioId = usuarioAtual.UsuarioId
            ?? throw new RegraDeNegocioException("NAO_AUTENTICADO", "Não autenticado.", TipoErro.NaoAutenticado);
        var etapa = await ObterOuFalharAsync(etapaId, ct);

        if (etapa.Status != StatusEtapa.EmAndamento)
        {
            throw new RegraDeNegocioException(
                "STATUS_INVALIDO", "A perda só pode ser registrada em uma etapa em andamento.", TipoErro.Conflito);
        }

        if (request.Quantidade > etapa.QuantidadeOp)
        {
            throw new RegraDeNegocioException(
                "PERDA_ACIMA_DA_QUANTIDADE",
                $"A perda não pode passar da quantidade da ordem de produção ({etapa.QuantidadeOp}).",
                TipoErro.EntidadeNaoProcessavel);
        }

        PerdaRequestValidator.TentarObterTipo(request.Tipo, out var tipo);
        var perda = new RegistroPerda(etapa.Id, tipo, request.Quantidade, request.Motivo.Trim(), usuarioId);
        producao.AdicionarPerda(perda);
        await unitOfWork.SalvarAsync(ct);

        return new PerdaDto(
            perda.Id,
            etapa.Id,
            etapa.EstacaoNome,
            perda.Tipo.ToString(),
            perda.Quantidade,
            perda.Motivo,
            usuarioId,
            UsuarioNome: null,
            perda.CriadoEm);
    }

    private async Task<EtapaResumo> ObterOuFalharAsync(int etapaId, CancellationToken ct) =>
        await producao.ObterEtapaAsync(etapaId, ct)
        ?? throw new RegraDeNegocioException("ETAPA_NAO_ENCONTRADA", "Etapa não encontrada.", TipoErro.NaoEncontrado);
}
