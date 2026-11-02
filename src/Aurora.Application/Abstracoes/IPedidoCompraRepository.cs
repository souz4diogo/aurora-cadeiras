using Aurora.Application.Compras;
using Aurora.Domain.Entidades;

namespace Aurora.Application.Abstracoes;

public interface IPedidoCompraRepository
{
    /// <summary>O pedido com os itens, sem rastreamento: sempre reflete o que o banco tem agora (inclusive após as procedures).</summary>
    Task<PedidoCompra?> ObterAsync(int id, CancellationToken ct = default);

    Task<(IReadOnlyList<PedidoCompraResumo> Itens, int Total)> ListarAsync(
        FiltroDePedidosCompra filtro, int pagina, int tamanho, CancellationToken ct = default);

    void Adicionar(PedidoCompra pedido);

    /// <summary>
    /// Solicitado → Emitido numa única instrução condicional. Falso se o pedido mudou de situação no meio do caminho,
    /// o que evita que duas pessoas decidam ao mesmo tempo com base numa leitura velha.
    /// </summary>
    Task<bool> TentarEmitirAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Cancela numa única instrução condicional: só se ainda estiver Solicitado ou Emitido e sem nada recebido. Falso
    /// se um recebimento chegou primeiro, e então o pedido NÃO é cancelado (o material já entrou no estoque).
    /// </summary>
    Task<bool> TentarCancelarAsync(int id, CancellationToken ct = default);

    /// <summary>Id do pedido dono do item; nulo se o item não existe.</summary>
    Task<int?> ObterPedidoDoItemAsync(int itemId, CancellationToken ct = default);

    /// <summary>Executa sp_ReceberItemCompra: atualiza o item, dá entrada no estoque, o custo e o status do pedido (RF17).</summary>
    Task ReceberItemAsync(int itemId, decimal quantidade, CancellationToken ct = default);
}
