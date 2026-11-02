using Aurora.Application.Qualidade;
using Aurora.Domain.Enums;

namespace Aurora.Application.Abstracoes;

public interface IQualidadeRepository
{
    /// <summary>
    /// Executa sp_RegistrarInspecao (RN06): aprovada, conclui a OP, dá entrada no estoque e pode deixar o pedido Pronto;
    /// reprovada, reabre as etapas a partir da estação de retrabalho.
    /// </summary>
    Task RegistrarInspecaoAsync(
        int ordemProducaoId,
        ResultadoInspecao resultado,
        byte? estacaoRetrabalhoId,
        string? observacao,
        CancellationToken ct = default);

    /// <summary>A inspeção mais recente da OP; nula se ela nunca foi inspecionada.</summary>
    Task<InspecaoResumo?> ObterUltimaDaOrdemAsync(int ordemProducaoId, CancellationToken ct = default);

    Task<(IReadOnlyList<InspecaoResumo> Itens, int Total)> ListarAsync(
        FiltroDeInspecoes filtro, int pagina, int tamanho, CancellationToken ct = default);
}
