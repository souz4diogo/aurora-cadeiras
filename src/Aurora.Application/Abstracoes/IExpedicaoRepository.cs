using Aurora.Application.Expedicoes;

namespace Aurora.Application.Abstracoes;

public interface IExpedicaoRepository
{
    /// <summary>Executa sp_Expedir (RN07): só pedido Pronto sai; baixa o produto acabado e muda o pedido para Expedido.</summary>
    Task ExpedirAsync(int pedidoId, string? transportadora, string? codigoRastreio, CancellationToken ct = default);

    Task<ExpedicaoResumo?> ObterPorPedidoAsync(int pedidoId, CancellationToken ct = default);

    Task<(IReadOnlyList<ExpedicaoResumo> Itens, int Total)> ListarAsync(
        FiltroDeExpedicoes filtro, int pagina, int tamanho, CancellationToken ct = default);
}
