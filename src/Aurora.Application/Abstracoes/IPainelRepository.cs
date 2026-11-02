using Aurora.Application.Painel;

namespace Aurora.Application.Abstracoes;

/// <summary>Consultas somente leitura do painel gerencial, sobre as views do banco (docs/05 §5).</summary>
public interface IPainelRepository
{
    /// <summary>Contagens de agora; o "hoje" dos atrasos é a data UTC do banco, a mesma das views.</summary>
    Task<ResumoDoPainelDto> ObterResumoAsync(DateTime agoraUtc, CancellationToken ct = default);

    /// <summary>Uma linha por estação que tem OP agora; as sem OP não vêm (o serviço as completa com zeros).</summary>
    Task<IReadOnlyList<ProducaoDaEstacaoDto>> ListarProducaoPorEstacaoAsync(CancellationToken ct = default);

    /// <summary>Do mais atrasado ao menos; <paramref name="vendedorId"/> restringe aos pedidos de um vendedor.</summary>
    Task<(IReadOnlyList<PedidoAtrasadoDto> Itens, int Total)> ListarPedidosAtrasadosAsync(
        int? vendedorId, int pagina, int tamanho, CancellationToken ct = default);

    /// <summary>Consumo por matéria-prima de <paramref name="de"/> a <paramref name="ate"/> (inclusive), do maior ao menor.</summary>
    Task<IReadOnlyList<ConsumoDeMaterialDto>> ListarConsumoAsync(DateOnly de, DateOnly ate, int limite, CancellationToken ct = default);
}
