using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Qualidade;

public sealed record InspecaoResposta(
    int Id,
    int OrdemProducaoId,
    string NumeroOp,
    string PedidoNumero,
    string ProdutoNome,
    string Resultado,
    byte? EstacaoRetrabalhoId,
    string? EstacaoRetrabalhoNome,
    string? Observacao,
    int InspetorId,
    string InspetorNome,
    DateTime DataHora,
    string StatusOrdem,
    string StatusPedido);

public interface IQualidadeApi
{
    /// <summary>Inspeções de uma OP, da mais recente à mais antiga (as reprovadas mostram o retrabalho pedido).</summary>
    Task<Pagina<InspecaoResposta>> ListarDaOrdemAsync(int ordemProducaoId, CancellationToken ct = default);
}

public sealed class QualidadeApi(HttpClient http) : ApiClientBase(http), IQualidadeApi
{
    public Task<Pagina<InspecaoResposta>> ListarDaOrdemAsync(int ordemProducaoId, CancellationToken ct = default) =>
        HttpGetAsync<Pagina<InspecaoResposta>>(
            "api/v1/inspecoes" + Consulta(("ordemProducaoId", ordemProducaoId), ("page", 1), ("pageSize", 50)), ct);
}
