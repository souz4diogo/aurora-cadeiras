using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Estacoes;

public sealed class EstacoesApi(HttpClient http) : ApiClientBase(http), IEstacoesApi
{
    public Task<IReadOnlyList<EstacaoResposta>> ListarAsync(CancellationToken ct = default) =>
        HttpGetAsync<IReadOnlyList<EstacaoResposta>>("api/v1/estacoes", ct);
}
