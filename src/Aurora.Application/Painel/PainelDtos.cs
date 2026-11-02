namespace Aurora.Application.Painel;

/// <summary>Cartões do painel gerencial (docs/07 §3.7): o retrato de agora da fábrica.</summary>
public sealed record ResumoDoPainelDto(
    int PedidosEmAberto,
    int PedidosAtrasados,
    int PedidosProntosParaExpedir,
    int OrdensPlanejadas,
    int OrdensEmAndamento,
    int OrdensAtrasadas,
    int MateriaisCriticos,
    int ComprasAReceber,
    int ComprasAtrasadas,
    DateTime AtualizadoEm);

/// <summary>
/// Situação de uma estação: as OPs que estão nela agora (a estação de uma OP é a da primeira etapa ainda não
/// concluída, a mesma regra do quadro de produção).
/// </summary>
public sealed record ProducaoDaEstacaoDto(
    byte EstacaoId,
    string Estacao,
    byte Ordem,
    int OrdensNaEstacao,
    int EmAndamento,
    int Aguardando,
    int Atrasadas,
    double? TempoMedioNaEtapaMinutos);

public sealed record PedidoAtrasadoDto(
    int PedidoId,
    string Numero,
    string Cliente,
    DateOnly PrazoEntrega,
    string Status,
    decimal ValorTotal,
    int DiasAtraso);

/// <summary>Consumo de uma matéria-prima pela produção no período.</summary>
public sealed record ConsumoDeMaterialDto(
    int MateriaPrimaId,
    string Codigo,
    string Nome,
    string Unidade,
    decimal Quantidade,
    decimal CustoEstimado);
