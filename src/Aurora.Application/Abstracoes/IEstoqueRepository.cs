using Aurora.Application.Estoque;
using Aurora.Domain.Entidades;

namespace Aurora.Application.Abstracoes;

public interface IEstoqueRepository
{
    /// <summary>Extrato de movimentos, do mais recente ao mais antigo.</summary>
    Task<(IReadOnlyList<MovimentoEstoqueResumo> Itens, int Total)> ListarMovimentosAsync(
        FiltroDeMovimentos filtro, int pagina, int tamanho, CancellationToken ct = default);

    /// <summary>Ajusta o saldo da matéria-prima pelo sp_AjustarEstoque, que registra a diferença como novo movimento.</summary>
    Task AjustarMateriaPrimaAsync(int materiaPrimaId, decimal novaQuantidade, string motivo, CancellationToken ct = default);

    /// <summary>Matérias-primas ativas por nome, com a indicação de saldo no mínimo ou abaixo.</summary>
    Task<(IReadOnlyList<SaldoDeMateriaPrimaDto> Itens, int Total)> ListarSaldosDeMateriaPrimaAsync(
        string? busca, bool? critico, int pagina, int tamanho, CancellationToken ct = default);

    /// <summary>Produtos ativos por código; produto sem linha de estoque aparece zerado.</summary>
    Task<(IReadOnlyList<SaldoDeProdutoDto> Itens, int Total)> ListarSaldosDeProdutoAsync(
        string? busca, int pagina, int tamanho, CancellationToken ct = default);

    /// <summary>Alertas ainda não lidos, o mais antigo primeiro (o que espera há mais tempo).</summary>
    Task<(IReadOnlyList<AlertaDeEstoqueDto> Itens, int Total)> ListarAlertasAbertosAsync(
        int pagina, int tamanho, CancellationToken ct = default);

    Task<AlertaEstoque?> ObterAlertaAsync(int id, CancellationToken ct = default);

    /// <summary>Leitura de um alerta já com os dados do material.</summary>
    Task<AlertaDeEstoqueDto?> ObterAlertaDetalhadoAsync(int id, CancellationToken ct = default);
}
