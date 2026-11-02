using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Relatorios;

public sealed record RelatorioDisponivelResposta(string Tipo, string Titulo, bool AceitaPeriodo, bool AceitaStatus, bool AceitaAno);

public sealed record FiltroDeRelatorio(DateOnly? De, DateOnly? Ate, string? Status, int? Ano);

public interface IRelatoriosApi
{
    Task<IReadOnlyList<RelatorioDisponivelResposta>> ListarAsync(CancellationToken ct = default);

    /// <summary>Gera o relatório na API (<paramref name="formato"/> "pdf" ou "xlsx") e devolve o arquivo.</summary>
    Task<ArquivoBaixado> BaixarAsync(string tipo, string formato, FiltroDeRelatorio filtro, CancellationToken ct = default);
}

public sealed class RelatoriosApi(HttpClient http) : ApiClientBase(http), IRelatoriosApi
{
    public Task<IReadOnlyList<RelatorioDisponivelResposta>> ListarAsync(CancellationToken ct = default) =>
        HttpGetAsync<IReadOnlyList<RelatorioDisponivelResposta>>("api/v1/relatorios", ct);

    public Task<ArquivoBaixado> BaixarAsync(string tipo, string formato, FiltroDeRelatorio filtro, CancellationToken ct = default) =>
        HttpBaixarAsync(
            $"api/v1/relatorios/{Uri.EscapeDataString(tipo)}" + Consulta(
                ("formato", formato), ("de", filtro.De), ("ate", filtro.Ate), ("status", filtro.Status), ("ano", filtro.Ano)),
            ct);
}
