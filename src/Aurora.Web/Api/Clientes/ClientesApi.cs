using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Clientes;

public sealed class ClientesApi(HttpClient http) : ApiClientBase(http), IClientesApi
{
    public Task<Pagina<ClienteResposta>> ListarAsync(string? busca, bool? ativo, int pagina, CancellationToken ct = default) =>
        HttpGetAsync<Pagina<ClienteResposta>>(
            "api/v1/clientes" + Consulta(("busca", busca), ("ativo", ativo), ("page", pagina), ("pageSize", TamanhoDePagina.Padrao)), ct);

    public Task<IReadOnlyList<ClienteResposta>> ListarAtivosAsync(CancellationToken ct = default) =>
        TodasAsPaginasAsync<ClienteResposta>("api/v1/clientes", [("ativo", true)], ct);

    public Task<ClienteResposta> ObterAsync(int id, CancellationToken ct = default) =>
        HttpGetAsync<ClienteResposta>($"api/v1/clientes/{id}", ct);

    public Task<ClienteResposta> CriarAsync(ClienteRequisicao dados, CancellationToken ct = default) =>
        HttpPostAsync<ClienteResposta>("api/v1/clientes", dados, ct);

    public Task<ClienteResposta> AtualizarAsync(int id, ClienteRequisicao dados, CancellationToken ct = default) =>
        HttpPutAsync<ClienteResposta>($"api/v1/clientes/{id}", dados, ct);

    public Task<ClienteResposta> AlterarAtivoAsync(int id, bool ativo, CancellationToken ct = default) =>
        HttpPatchAsync<ClienteResposta>($"api/v1/clientes/{id}/ativo", new { Ativo = ativo }, ct);
}
