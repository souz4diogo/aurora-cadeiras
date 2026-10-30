using Aurora.Domain.Entidades;

namespace Aurora.Application.Abstracoes;

public interface IFichaTecnicaRepository
{
    Task<IReadOnlyList<FichaTecnicaItem>> ListarPorProdutoAsync(int produtoId, CancellationToken ct = default);

    /// <summary>Troca a ficha inteira do produto de forma atômica (tudo ou nada), com sua própria transação.</summary>
    Task SubstituirAsync(int produtoId, IReadOnlyList<FichaTecnicaItem> itens, CancellationToken ct = default);
}
