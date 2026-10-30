using Aurora.Domain.Entidades;

namespace Aurora.Application.Abstracoes;

public interface IRoteiroRepository
{
    /// <summary>Etapas do roteiro do produto, em ordem de sequência.</summary>
    Task<IReadOnlyList<RoteiroProduto>> ListarPorProdutoAsync(int produtoId, CancellationToken ct = default);

    /// <summary>Troca o roteiro inteiro do produto de forma atômica (tudo ou nada), com sua própria transação.</summary>
    Task SubstituirAsync(int produtoId, IReadOnlyList<RoteiroProduto> etapas, CancellationToken ct = default);
}
