using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Estoque;

public sealed class EstoqueApi(HttpClient http) : ApiClientBase(http), IEstoqueApi
{
    public Task<Pagina<SaldoDeMateriaPrimaResposta>> ListarSaldosDeMateriaPrimaAsync(
        string? busca, bool? critico, int pagina, CancellationToken ct = default) =>
        HttpGetAsync<Pagina<SaldoDeMateriaPrimaResposta>>(
            "api/v1/estoque/materias-primas" + Consulta(
                ("busca", busca), ("critico", critico), ("page", pagina), ("pageSize", TamanhoDePagina.Padrao)),
            ct);

    public Task<Pagina<SaldoDeProdutoResposta>> ListarSaldosDeProdutoAsync(string? busca, int pagina, CancellationToken ct = default) =>
        HttpGetAsync<Pagina<SaldoDeProdutoResposta>>(
            "api/v1/estoque/produtos" + Consulta(("busca", busca), ("page", pagina), ("pageSize", TamanhoDePagina.Padrao)),
            ct);

    public Task<Pagina<MovimentoEstoqueResposta>> ListarMovimentosAsync(
        FiltroDeMovimentos filtro, int pagina, CancellationToken ct = default) =>
        HttpGetAsync<Pagina<MovimentoEstoqueResposta>>(
            "api/v1/estoque/movimentos" + Consulta(
                ("materiaPrimaId", filtro.MateriaPrimaId), ("tipo", filtro.Tipo), ("origem", filtro.Origem),
                ("de", filtro.De), ("ate", filtro.Ate), ("page", pagina), ("pageSize", TamanhoDePagina.Padrao)),
            ct);

    public Task<AjusteDeEstoqueResposta> AjustarAsync(AjusteDeEstoqueRequisicao dados, CancellationToken ct = default) =>
        HttpPostAsync<AjusteDeEstoqueResposta>("api/v1/estoque/ajustes", dados, ct);

    public Task<Pagina<AlertaDeEstoqueResposta>> ListarAlertasAsync(int pagina, CancellationToken ct = default) =>
        HttpGetAsync<Pagina<AlertaDeEstoqueResposta>>(
            "api/v1/estoque/alertas" + Consulta(("page", pagina), ("pageSize", TamanhoDePagina.Padrao)), ct);

    public Task<AlertaDeEstoqueResposta> MarcarAlertaComoLidoAsync(int id, CancellationToken ct = default) =>
        HttpPostAsync<AlertaDeEstoqueResposta>($"api/v1/estoque/alertas/{id}/ler", new { }, ct);
}
