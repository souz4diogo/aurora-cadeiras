using Aurora.Domain.Entidades;

namespace Aurora.Application.Abstracoes;

public interface IMateriaPrimaRepository
{
    Task<MateriaPrima?> ObterPorIdAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<MateriaPrima>> ObterPorIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct = default);

    /// <summary>Se a matéria-prima já tem movimento de estoque ou consta em alguma ficha técnica.</summary>
    Task<bool> EmUsoAsync(int id, CancellationToken ct = default);

    Task<bool> CodigoExisteAsync(string codigo, int? ignorarId, CancellationToken ct = default);

    Task<bool> FornecedorExisteAsync(int fornecedorId, CancellationToken ct = default);

    Task<(IReadOnlyList<MateriaPrima> Itens, int Total)> ListarAsync(
        string? busca, bool? ativo, int pagina, int tamanho, CancellationToken ct = default);

    void Adicionar(MateriaPrima materiaPrima);
}
