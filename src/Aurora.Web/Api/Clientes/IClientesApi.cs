using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Clientes;

public interface IClientesApi
{
    Task<Pagina<ClienteResposta>> ListarAsync(string? busca, bool? ativo, int pagina, CancellationToken ct = default);

    /// <summary>Todos os clientes ativos, em ordem alfabética, para listas de escolha (ex.: cliente do pedido).</summary>
    Task<IReadOnlyList<ClienteResposta>> ListarAtivosAsync(CancellationToken ct = default);

    Task<ClienteResposta> ObterAsync(int id, CancellationToken ct = default);

    Task<ClienteResposta> CriarAsync(ClienteRequisicao dados, CancellationToken ct = default);

    Task<ClienteResposta> AtualizarAsync(int id, ClienteRequisicao dados, CancellationToken ct = default);

    Task<ClienteResposta> AlterarAtivoAsync(int id, bool ativo, CancellationToken ct = default);
}
