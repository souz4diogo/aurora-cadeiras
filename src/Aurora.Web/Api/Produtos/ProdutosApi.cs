using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Produtos;

public sealed class ProdutosApi(HttpClient http) : ApiClientBase(http), IProdutosApi
{
    public Task<Pagina<ProdutoResposta>> ListarAsync(string? busca, bool? ativo, int pagina, CancellationToken ct = default) =>
        HttpGetAsync<Pagina<ProdutoResposta>>(
            "api/v1/produtos" + Consulta(("busca", busca), ("ativo", ativo), ("page", pagina), ("pageSize", TamanhoDePagina.Padrao)), ct);

    public Task<IReadOnlyList<ProdutoResposta>> ListarAtivosAsync(CancellationToken ct = default) =>
        TodasAsPaginasAsync<ProdutoResposta>("api/v1/produtos", [("ativo", true)], ct);

    public Task<ProdutoResposta> ObterAsync(int id, CancellationToken ct = default) =>
        HttpGetAsync<ProdutoResposta>($"api/v1/produtos/{id}", ct);

    public Task<ProdutoResposta> CriarAsync(ProdutoRequisicao dados, CancellationToken ct = default) =>
        HttpPostAsync<ProdutoResposta>("api/v1/produtos", dados, ct);

    public Task<ProdutoResposta> AtualizarAsync(int id, ProdutoRequisicao dados, CancellationToken ct = default) =>
        HttpPutAsync<ProdutoResposta>($"api/v1/produtos/{id}", dados, ct);

    public Task<ProdutoResposta> AlterarAtivoAsync(int id, bool ativo, CancellationToken ct = default) =>
        HttpPatchAsync<ProdutoResposta>($"api/v1/produtos/{id}/ativo", new { Ativo = ativo }, ct);

    public Task<FichaTecnicaResposta> ObterFichaTecnicaAsync(int produtoId, CancellationToken ct = default) =>
        HttpGetAsync<FichaTecnicaResposta>($"api/v1/produtos/{produtoId}/ficha-tecnica", ct);

    public Task<FichaTecnicaResposta> SubstituirFichaTecnicaAsync(int produtoId, FichaTecnicaRequisicao dados, CancellationToken ct = default) =>
        HttpPutAsync<FichaTecnicaResposta>($"api/v1/produtos/{produtoId}/ficha-tecnica", dados, ct);

    public Task<RoteiroResposta> ObterRoteiroAsync(int produtoId, CancellationToken ct = default) =>
        HttpGetAsync<RoteiroResposta>($"api/v1/produtos/{produtoId}/roteiro", ct);

    public Task<RoteiroResposta> SubstituirRoteiroAsync(int produtoId, RoteiroRequisicao dados, CancellationToken ct = default) =>
        HttpPutAsync<RoteiroResposta>($"api/v1/produtos/{produtoId}/roteiro", dados, ct);
}
