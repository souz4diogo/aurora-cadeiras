using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Fornecedores;

public sealed class FornecedoresApi(HttpClient http) : ApiClientBase(http), IFornecedoresApi
{
    private const int LimiteParaEscolha = 100;

    public Task<Pagina<FornecedorResposta>> ListarAsync(
        string? busca, bool? ativo, bool? comunidadeTradicional, int pagina, CancellationToken ct = default) =>
        HttpGetAsync<Pagina<FornecedorResposta>>(
            "api/v1/fornecedores" + Consulta(
                ("busca", busca), ("ativo", ativo), ("comunidadeTradicional", comunidadeTradicional),
                ("page", pagina), ("pageSize", TamanhoDePagina.Padrao)),
            ct);

    public async Task<IReadOnlyList<FornecedorResposta>> ListarAtivosAsync(CancellationToken ct = default) =>
        (await HttpGetAsync<Pagina<FornecedorResposta>>(
            "api/v1/fornecedores" + Consulta(("ativo", true), ("page", 1), ("pageSize", LimiteParaEscolha)), ct)).Items;

    public Task<FornecedorResposta> ObterAsync(int id, CancellationToken ct = default) =>
        HttpGetAsync<FornecedorResposta>($"api/v1/fornecedores/{id}", ct);

    public Task<FornecedorResposta> CriarAsync(FornecedorRequisicao dados, CancellationToken ct = default) =>
        HttpPostAsync<FornecedorResposta>("api/v1/fornecedores", dados, ct);

    public Task<FornecedorResposta> AtualizarAsync(int id, FornecedorRequisicao dados, CancellationToken ct = default) =>
        HttpPutAsync<FornecedorResposta>($"api/v1/fornecedores/{id}", dados, ct);

    public Task<FornecedorResposta> AlterarAtivoAsync(int id, bool ativo, CancellationToken ct = default) =>
        HttpPatchAsync<FornecedorResposta>($"api/v1/fornecedores/{id}/ativo", new { Ativo = ativo }, ct);
}
