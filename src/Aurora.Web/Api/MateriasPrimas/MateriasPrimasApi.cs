using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.MateriasPrimas;

public sealed class MateriasPrimasApi(HttpClient http) : ApiClientBase(http), IMateriasPrimasApi
{
    public Task<Pagina<MateriaPrimaResposta>> ListarAsync(string? busca, bool? ativo, int pagina, CancellationToken ct = default) =>
        HttpGetAsync<Pagina<MateriaPrimaResposta>>(
            "api/v1/materias-primas" + Consulta(("busca", busca), ("ativo", ativo), ("page", pagina), ("pageSize", TamanhoDePagina.Padrao)), ct);

    public Task<IReadOnlyList<MateriaPrimaResposta>> ListarAtivasAsync(CancellationToken ct = default) =>
        TodasAsPaginasAsync<MateriaPrimaResposta>("api/v1/materias-primas", [("ativo", true)], ct);

    public Task<MateriaPrimaResposta> ObterAsync(int id, CancellationToken ct = default) =>
        HttpGetAsync<MateriaPrimaResposta>($"api/v1/materias-primas/{id}", ct);

    public Task<MateriaPrimaResposta> CriarAsync(MateriaPrimaRequisicao dados, CancellationToken ct = default) =>
        HttpPostAsync<MateriaPrimaResposta>("api/v1/materias-primas", dados, ct);

    public Task<MateriaPrimaResposta> AtualizarAsync(int id, MateriaPrimaRequisicao dados, CancellationToken ct = default) =>
        HttpPutAsync<MateriaPrimaResposta>($"api/v1/materias-primas/{id}", dados, ct);

    public Task<MateriaPrimaResposta> AlterarAtivoAsync(int id, bool ativo, CancellationToken ct = default) =>
        HttpPatchAsync<MateriaPrimaResposta>($"api/v1/materias-primas/{id}/ativo", new { Ativo = ativo }, ct);
}
