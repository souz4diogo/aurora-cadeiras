using Aurora.Domain.Enums;

namespace Aurora.Application.Producao;

/// <param name="VendedorId">Quando informado, restringe às OPs de pedidos desse vendedor.</param>
/// <param name="EstacaoId">Estação onde a OP está agora (a primeira etapa ainda não concluída).</param>
/// <param name="Atrasadas">Verdadeiro: só as em aberto com a previsão vencida; falso: as demais.</param>
/// <param name="Hoje">Data de referência do atraso.</param>
/// <param name="SomenteAbertas">
/// Só as Planejadas e Em andamento, da previsão mais próxima à mais distante: a ordem do quadro de produção,
/// em que o que vence antes aparece primeiro.
/// </param>
public sealed record FiltroDeOrdens(
    int? VendedorId,
    StatusOrdemProducao? Status,
    byte? EstacaoId,
    int? PedidoId,
    bool? Atrasadas,
    DateOnly Hoje,
    bool SomenteAbertas = false);
