using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Expedicoes;

public sealed record ExpedicaoResposta(
    int Id,
    int PedidoId,
    string PedidoNumero,
    string ClienteNome,
    int UsuarioId,
    string UsuarioNome,
    string? Transportadora,
    string? CodigoRastreio,
    DateTime DataHora);

public sealed record ExpedirRequisicao(string? Transportadora, string? CodigoRastreio);

public interface IExpedicoesApi
{
    Task<Pagina<ExpedicaoResposta>> ListarAsync(int pagina, CancellationToken ct = default);

    /// <summary>Expede o pedido Pronto: libera a reserva, baixa o produto acabado e passa o pedido a Expedido (RN07).</summary>
    Task<ExpedicaoResposta> ExpedirAsync(int pedidoId, ExpedirRequisicao dados, CancellationToken ct = default);
}

public sealed class ExpedicoesApi(HttpClient http) : ApiClientBase(http), IExpedicoesApi
{
    public Task<Pagina<ExpedicaoResposta>> ListarAsync(int pagina, CancellationToken ct = default) =>
        HttpGetAsync<Pagina<ExpedicaoResposta>>(
            "api/v1/expedicoes" + Consulta(("page", pagina), ("pageSize", TamanhoDePagina.Padrao)), ct);

    public Task<ExpedicaoResposta> ExpedirAsync(int pedidoId, ExpedirRequisicao dados, CancellationToken ct = default) =>
        HttpPostAsync<ExpedicaoResposta>($"api/v1/pedidos/{pedidoId}/expedir", dados, ct);
}
