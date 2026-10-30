using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.MateriasPrimas;

public interface IMateriasPrimasApi
{
    Task<Pagina<MateriaPrimaResposta>> ListarAsync(string? busca, bool? ativo, int pagina, CancellationToken ct = default);

    Task<MateriaPrimaResposta> ObterAsync(int id, CancellationToken ct = default);

    Task<MateriaPrimaResposta> CriarAsync(MateriaPrimaRequisicao dados, CancellationToken ct = default);

    Task<MateriaPrimaResposta> AtualizarAsync(int id, MateriaPrimaRequisicao dados, CancellationToken ct = default);

    Task<MateriaPrimaResposta> AlterarAtivoAsync(int id, bool ativo, CancellationToken ct = default);
}
