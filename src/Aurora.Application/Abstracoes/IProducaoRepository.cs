using Aurora.Application.Producao;
using Aurora.Domain.Entidades;

namespace Aurora.Application.Abstracoes;

public interface IProducaoRepository
{
    /// <summary>OPs da mais recente à mais antiga, cada uma com a etapa em que está.</summary>
    Task<(IReadOnlyList<OrdemProducaoResumo> Itens, int Total)> ListarOrdensAsync(
        FiltroDeOrdens filtro, int pagina, int tamanho, CancellationToken ct = default);

    /// <summary>A OP com etapas, apontamentos e perdas; nula se não existir ou fugir da restrição de vendedor.</summary>
    Task<OrdemProducaoDetalhe?> ObterOrdemAsync(int id, int? vendedorId, CancellationToken ct = default);

    /// <summary>Id da OP dona do código do QR; nulo se nenhuma OP tem esse código.</summary>
    Task<int?> ObterOrdemIdPorQrAsync(Guid codigoQr, CancellationToken ct = default);

    /// <summary>Etapas pendentes ou em andamento da estação, de OPs em aberto, das mais urgentes às menos.</summary>
    Task<IReadOnlyList<EtapaDaEstacao>> ListarEtapasDaEstacaoAsync(byte estacaoId, CancellationToken ct = default);

    /// <summary>Já existe apontamento com este <c>ClientRequestId</c> (reenvio de lote offline)?</summary>
    Task<bool> ApontamentoRegistradoAsync(Guid clientRequestId, CancellationToken ct = default);

    Task<EtapaResumo?> ObterEtapaAsync(int etapaId, CancellationToken ct = default);

    /// <summary>Executa sp_IniciarEtapa: confere a sequência (RN05), baixa a matéria-prima (RN03/RN04) e aponta o início.</summary>
    Task IniciarEtapaAsync(int etapaId, Guid clientRequestId, DateTime dataHoraClienteUtc, CancellationToken ct = default);

    /// <summary>Executa sp_FinalizarEtapa.</summary>
    Task FinalizarEtapaAsync(int etapaId, Guid clientRequestId, DateTime dataHoraClienteUtc, CancellationToken ct = default);

    void AdicionarPerda(RegistroPerda perda);
}
