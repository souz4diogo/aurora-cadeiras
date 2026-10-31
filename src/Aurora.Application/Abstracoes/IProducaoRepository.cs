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

    Task<EtapaResumo?> ObterEtapaAsync(int etapaId, CancellationToken ct = default);

    /// <summary>Executa sp_IniciarEtapa: confere a sequência (RN05), baixa a matéria-prima (RN03/RN04) e aponta o início.</summary>
    Task IniciarEtapaAsync(int etapaId, Guid clientRequestId, DateTime dataHoraClienteUtc, CancellationToken ct = default);

    /// <summary>Executa sp_FinalizarEtapa.</summary>
    Task FinalizarEtapaAsync(int etapaId, Guid clientRequestId, DateTime dataHoraClienteUtc, CancellationToken ct = default);

    void AdicionarPerda(RegistroPerda perda);
}
