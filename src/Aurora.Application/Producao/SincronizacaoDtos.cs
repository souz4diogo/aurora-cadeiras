namespace Aurora.Application.Producao;

/// <summary>
/// Um apontamento feito no aparelho, possivelmente offline. <paramref name="Tipo"/> é "Inicio" ou "Fim";
/// <paramref name="DataHoraCliente"/> é a hora da ação no aparelho.
/// </summary>
public sealed record ApontamentoOfflineItem(Guid ClientRequestId, string Tipo, int EtapaId, DateTimeOffset DataHoraCliente);

/// <summary>Lote de apontamentos da fila local, na ordem em que foram feitos.</summary>
public sealed record SincronizacaoRequest(IReadOnlyList<ApontamentoOfflineItem> Itens);

/// <summary>Situações possíveis de cada item do lote (docs/06 §5).</summary>
public static class StatusDeSincronizacao
{
    public const string Aplicado = "Aplicado";
    public const string JaProcessado = "JaProcessado";
    public const string Rejeitado = "Rejeitado";
}

public sealed record ResultadoDeSincronizacaoDto(Guid ClientRequestId, string Status, string? Code, string? Mensagem)
{
    public static ResultadoDeSincronizacaoDto Aplicado(Guid id) => new(id, StatusDeSincronizacao.Aplicado, null, null);

    public static ResultadoDeSincronizacaoDto JaProcessado(Guid id) => new(id, StatusDeSincronizacao.JaProcessado, null, null);

    public static ResultadoDeSincronizacaoDto Rejeitado(Guid id, string code, string mensagem) =>
        new(id, StatusDeSincronizacao.Rejeitado, code, mensagem);
}

/// <summary>Um resultado por item, na mesma ordem do lote enviado.</summary>
public sealed record SincronizacaoResponse(IReadOnlyList<ResultadoDeSincronizacaoDto> Resultados);
