using Aurora.Application.Painel;
using Aurora.Application.Relatorios;
using Aurora.Domain.Enums;

namespace Aurora.Application.Abstracoes;

/// <summary>
/// Consultas somente leitura dos relatórios (RF21). Cada uma recebe um limite de linhas e devolve também o total, para o
/// relatório avisar quando cortou, em vez de montar um arquivo gigante ou calar.
/// </summary>
public interface IRelatoriosRepository
{
    /// <summary>Pedidos pela data do pedido, do mais recente ao mais antigo; <paramref name="vendedorId"/> restringe a um vendedor.</summary>
    Task<ResultadoLimitado<LinhaDePedido>> ListarPedidosAsync(
        int? vendedorId, StatusPedido? status, DateOnly? de, DateOnly? ate, int limite, CancellationToken ct = default);

    /// <summary>OPs em aberto, cada uma na estação em que está agora (primeira etapa não concluída).</summary>
    Task<ResultadoLimitado<LinhaDeProducao>> ListarProducaoAsync(int limite, CancellationToken ct = default);

    Task<ResultadoLimitado<LinhaDeEstoqueDeMateria>> ListarEstoqueDeMateriasAsync(int limite, CancellationToken ct = default);

    Task<ResultadoLimitado<LinhaDeEstoqueDeProduto>> ListarEstoqueDeProdutosAsync(int limite, CancellationToken ct = default);

    Task<IReadOnlyList<ConsumoDeMaterialDto>> ListarConsumoPorMateriaAsync(DateOnly de, DateOnly ate, int limite, CancellationToken ct = default);

    Task<IReadOnlyList<LinhaDeConsumoPorProduto>> ListarConsumoPorProdutoAsync(DateOnly de, DateOnly ate, int limite, CancellationToken ct = default);

    /// <summary>Recebimentos de compra por mês e a parte dos fornecedores de comunidades tradicionais (Etapa 3).</summary>
    Task<IReadOnlyList<LinhaDeComprasComunitarias>> ListarComprasComunitariasAsync(int? ano, CancellationToken ct = default);
}
