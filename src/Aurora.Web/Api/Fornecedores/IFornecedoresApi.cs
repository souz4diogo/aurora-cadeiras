using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Fornecedores;

public interface IFornecedoresApi
{
    Task<Pagina<FornecedorResposta>> ListarAsync(
        string? busca, bool? ativo, bool? comunidadeTradicional, int pagina, CancellationToken ct = default);

    /// <summary>Fornecedores ativos, em ordem alfabética, para preencher listas de escolha (até 100).</summary>
    Task<IReadOnlyList<FornecedorResposta>> ListarAtivosAsync(CancellationToken ct = default);

    Task<FornecedorResposta> ObterAsync(int id, CancellationToken ct = default);

    Task<FornecedorResposta> CriarAsync(FornecedorRequisicao dados, CancellationToken ct = default);

    Task<FornecedorResposta> AtualizarAsync(int id, FornecedorRequisicao dados, CancellationToken ct = default);

    Task<FornecedorResposta> AlterarAtivoAsync(int id, bool ativo, CancellationToken ct = default);
}
