using Aurora.Application.Abstracoes;
using Aurora.Application.Comum;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Aurora.Application.Producao;

/// <summary>
/// Sincronização de apontamentos feitos offline (RF23). Cada item roda pelo mesmo caminho do início e do fim online
/// (<see cref="EtapaService"/>), em ordem e de forma independente: um item rejeitado não derruba os demais, e reenviar
/// um <c>ClientRequestId</c> já aplicado devolve <c>JaProcessado</c> sem repetir o efeito.
/// </summary>
public partial class ApontamentoService(
    EtapaService etapas,
    IProducaoRepository producao,
    ICurrentUser usuarioAtual,
    IValidator<SincronizacaoRequest> validadorDoLote,
    ILogger<ApontamentoService> log)
{
    /// <summary>Código do item que falhou por um motivo que não é regra de negócio (deadlock, timeout...).</summary>
    public const string CodigoDeErroInterno = "ERRO_INTERNO";

    public async Task<SincronizacaoResponse> SincronizarAsync(SincronizacaoRequest request, CancellationToken ct = default)
    {
        await validadorDoLote.ValidateAndThrowAsync(request, ct);

        var resultados = new List<ResultadoDeSincronizacaoDto>(request.Itens.Count);
        foreach (var item in request.Itens)
        {
            resultados.Add(await ProcessarAsync(item, ct));
        }

        return new SincronizacaoResponse(resultados);
    }

    private async Task<ResultadoDeSincronizacaoDto> ProcessarAsync(ApontamentoOfflineItem? item, CancellationToken ct)
    {
        if (item is null)
        {
            return ResultadoDeSincronizacaoDto.Rejeitado(Guid.Empty, "VALIDACAO", "Item vazio no lote.");
        }

        if (!EnumPorNome.TentarObter(item.Tipo, out TipoApontamento tipo))
        {
            return ResultadoDeSincronizacaoDto.Rejeitado(
                item.ClientRequestId, "VALIDACAO", "O tipo do apontamento deve ser Inicio ou Fim.");
        }

        if (item.ClientRequestId == Guid.Empty)
        {
            return ResultadoDeSincronizacaoDto.Rejeitado(
                item.ClientRequestId, "VALIDACAO", "Informe o identificador da requisição.");
        }

        // O inspetor não finaliza etapa: a de Qualidade termina pela inspeção (mesma regra do endpoint /finalizar)
        if (tipo == TipoApontamento.Fim && usuarioAtual.Perfil != PerfilUsuario.Operador)
        {
            return ResultadoDeSincronizacaoDto.Rejeitado(
                item.ClientRequestId, "PERFIL_SEM_PERMISSAO", "Somente o operador finaliza etapas.");
        }

        if (await producao.ApontamentoRegistradoAsync(item.ClientRequestId, ct))
        {
            return ResultadoDeSincronizacaoDto.JaProcessado(item.ClientRequestId);
        }

        var apontamento = new ApontamentoRequest(item.ClientRequestId, item.DataHoraCliente);
        try
        {
            _ = tipo == TipoApontamento.Inicio
                ? await etapas.IniciarAsync(item.EtapaId, apontamento, ct)
                : await etapas.FinalizarAsync(item.EtapaId, apontamento, ct);
            return ResultadoDeSincronizacaoDto.Aplicado(item.ClientRequestId);
        }
        catch (RegraDeNegocioException ex)
        {
            return ResultadoDeSincronizacaoDto.Rejeitado(item.ClientRequestId, ex.Codigo, ex.Message);
        }
        catch (ValidationException ex)
        {
            var mensagem = ex.Errors.FirstOrDefault()?.ErrorMessage ?? "Apontamento inválido.";
            return ResultadoDeSincronizacaoDto.Rejeitado(item.ClientRequestId, "VALIDACAO", mensagem);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Um item com defeito não pode derrubar o lote nem ficar para sempre à frente da fila do aparelho: ele vira
            // Rejeitado e o usuário decide tentar de novo ou descartar. O reenvio é seguro (idempotente).
            LogMensagens.FalhaAoSincronizarItem(log, ex, item.EtapaId, tipo);
            return ResultadoDeSincronizacaoDto.Rejeitado(
                item.ClientRequestId, CodigoDeErroInterno, "Não foi possível processar este apontamento agora. Tente de novo.");
        }
    }

    private static partial class LogMensagens
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "Falha inesperada ao sincronizar o apontamento da etapa {EtapaId} ({Tipo})")]
        public static partial void FalhaAoSincronizarItem(ILogger logger, Exception ex, int etapaId, TipoApontamento tipo);
    }
}
