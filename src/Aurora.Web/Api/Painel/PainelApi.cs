using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Painel;

public sealed record ResumoDoPainelResposta(
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

public sealed record ProducaoDaEstacaoResposta(
    byte EstacaoId,
    string Estacao,
    byte Ordem,
    int OrdensNaEstacao,
    int EmAndamento,
    int Aguardando,
    int Atrasadas,
    double? TempoMedioNaEtapaMinutos);

public sealed record PedidoAtrasadoResposta(
    int PedidoId,
    string Numero,
    string Cliente,
    DateOnly PrazoEntrega,
    string Status,
    decimal ValorTotal,
    int DiasAtraso);

public sealed record ConsumoDeMaterialResposta(
    int MateriaPrimaId,
    string Codigo,
    string Nome,
    string Unidade,
    decimal Quantidade,
    decimal CustoEstimado);

public interface IPainelApi
{
    Task<ResumoDoPainelResposta> ObterResumoAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ProducaoDaEstacaoResposta>> ObterProducaoPorEstacaoAsync(CancellationToken ct = default);

    Task<Pagina<PedidoAtrasadoResposta>> ListarPedidosAtrasadosAsync(int pagina, CancellationToken ct = default);

    Task<IReadOnlyList<ConsumoDeMaterialResposta>> ObterConsumoAsync(int dias, CancellationToken ct = default);
}

public sealed class PainelApi(HttpClient http) : ApiClientBase(http), IPainelApi
{
    public Task<ResumoDoPainelResposta> ObterResumoAsync(CancellationToken ct = default) =>
        HttpGetAsync<ResumoDoPainelResposta>("api/v1/painel/resumo", ct);

    public Task<IReadOnlyList<ProducaoDaEstacaoResposta>> ObterProducaoPorEstacaoAsync(CancellationToken ct = default) =>
        HttpGetAsync<IReadOnlyList<ProducaoDaEstacaoResposta>>("api/v1/painel/producao-por-estacao", ct);

    public Task<Pagina<PedidoAtrasadoResposta>> ListarPedidosAtrasadosAsync(int pagina, CancellationToken ct = default) =>
        HttpGetAsync<Pagina<PedidoAtrasadoResposta>>(
            "api/v1/painel/pedidos-atrasados" + Consulta(("page", pagina), ("pageSize", TamanhoDePagina.Padrao)), ct);

    public Task<IReadOnlyList<ConsumoDeMaterialResposta>> ObterConsumoAsync(int dias, CancellationToken ct = default) =>
        HttpGetAsync<IReadOnlyList<ConsumoDeMaterialResposta>>("api/v1/painel/consumo-de-materiais" + Consulta(("dias", dias)), ct);
}
