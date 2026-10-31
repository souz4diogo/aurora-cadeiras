using Aurora.Application.Estoque;

namespace Aurora.Application.Abstracoes;

public interface IEstoqueRepository
{
    /// <summary>Extrato de movimentos, do mais recente ao mais antigo.</summary>
    Task<(IReadOnlyList<MovimentoEstoqueResumo> Itens, int Total)> ListarMovimentosAsync(
        FiltroDeMovimentos filtro, int pagina, int tamanho, CancellationToken ct = default);

    /// <summary>Ajusta o saldo da matéria-prima pelo sp_AjustarEstoque, que registra a diferença como novo movimento.</summary>
    Task AjustarMateriaPrimaAsync(int materiaPrimaId, decimal novaQuantidade, string motivo, CancellationToken ct = default);
}
