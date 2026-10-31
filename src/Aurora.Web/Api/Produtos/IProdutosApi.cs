using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Produtos;

public interface IProdutosApi
{
    Task<Pagina<ProdutoResposta>> ListarAsync(string? busca, bool? ativo, int pagina, CancellationToken ct = default);

    /// <summary>Todos os produtos ativos, em ordem alfabética, para listas de escolha (ex.: itens do pedido).</summary>
    Task<IReadOnlyList<ProdutoResposta>> ListarAtivosAsync(CancellationToken ct = default);

    Task<ProdutoResposta> ObterAsync(int id, CancellationToken ct = default);

    Task<ProdutoResposta> CriarAsync(ProdutoRequisicao dados, CancellationToken ct = default);

    Task<ProdutoResposta> AtualizarAsync(int id, ProdutoRequisicao dados, CancellationToken ct = default);

    Task<ProdutoResposta> AlterarAtivoAsync(int id, bool ativo, CancellationToken ct = default);

    Task<FichaTecnicaResposta> ObterFichaTecnicaAsync(int produtoId, CancellationToken ct = default);

    Task<FichaTecnicaResposta> SubstituirFichaTecnicaAsync(int produtoId, FichaTecnicaRequisicao dados, CancellationToken ct = default);

    Task<RoteiroResposta> ObterRoteiroAsync(int produtoId, CancellationToken ct = default);

    Task<RoteiroResposta> SubstituirRoteiroAsync(int produtoId, RoteiroRequisicao dados, CancellationToken ct = default);
}
