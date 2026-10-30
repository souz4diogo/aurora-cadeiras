using Aurora.Domain.Entidades;

namespace Aurora.Application.Abstracoes;

public interface IProdutoRepository
{
    Task<Produto?> ObterPorIdAsync(int id, CancellationToken ct = default);

    Task<bool> CodigoExisteAsync(string codigo, int? ignorarId, CancellationToken ct = default);

    Task<(IReadOnlyList<Produto> Itens, int Total)> ListarAsync(
        string? busca, bool? ativo, int pagina, int tamanho, CancellationToken ct = default);

    void Adicionar(Produto produto);
}
