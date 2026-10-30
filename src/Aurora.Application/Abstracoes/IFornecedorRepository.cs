using Aurora.Domain.Entidades;

namespace Aurora.Application.Abstracoes;

public interface IFornecedorRepository
{
    Task<Fornecedor?> ObterPorIdAsync(int id, CancellationToken ct = default);

    Task<bool> CnpjExisteAsync(string cnpj, int? ignorarId, CancellationToken ct = default);

    Task<(IReadOnlyList<Fornecedor> Itens, int Total)> ListarAsync(
        string? busca, bool? ativo, bool? comunidadeTradicional, int pagina, int tamanho, CancellationToken ct = default);

    void Adicionar(Fornecedor fornecedor);
}
