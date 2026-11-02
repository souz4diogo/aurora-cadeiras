using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Auditoria;

public sealed record CampoAlteradoResposta(string Campo, string? Antes, string? Depois);

public sealed record AuditoriaResposta(
    long Id,
    string Tabela,
    string Registro,
    string Acao,
    int? UsuarioId,
    string? UsuarioNome,
    DateTime DataHora,
    IReadOnlyList<CampoAlteradoResposta> Alteracoes);

public sealed record FiltroDeAuditoria(string? Tabela, string? Registro, int? UsuarioId, DateOnly? De, DateOnly? Ate);

public interface IAuditoriaApi
{
    Task<Pagina<AuditoriaResposta>> ListarAsync(FiltroDeAuditoria filtro, int pagina, CancellationToken ct = default);

    /// <summary>As tabelas que têm auditoria (a API é a fonte da lista).</summary>
    Task<IReadOnlyList<string>> ListarTabelasAsync(CancellationToken ct = default);
}

public sealed class AuditoriaApi(HttpClient http) : ApiClientBase(http), IAuditoriaApi
{
    public Task<Pagina<AuditoriaResposta>> ListarAsync(FiltroDeAuditoria filtro, int pagina, CancellationToken ct = default) =>
        HttpGetAsync<Pagina<AuditoriaResposta>>(
            "api/v1/auditoria" + Consulta(
                ("tabela", filtro.Tabela), ("registro", filtro.Registro), ("usuarioId", filtro.UsuarioId),
                ("de", filtro.De), ("ate", filtro.Ate), ("page", pagina), ("pageSize", TamanhoDePagina.Padrao)),
            ct);

    public Task<IReadOnlyList<string>> ListarTabelasAsync(CancellationToken ct = default) =>
        HttpGetAsync<IReadOnlyList<string>>("api/v1/auditoria/tabelas", ct);
}
