using Aurora.Application.Auditoria;

namespace Aurora.Application.Abstracoes;

public interface IAuditoriaRepository
{
    /// <summary>Histórico da mais recente à mais antiga, com o nome de quem alterou.</summary>
    Task<(IReadOnlyList<AuditoriaResumo> Itens, int Total)> ListarAsync(
        FiltroDeAuditoria filtro, int pagina, int tamanho, CancellationToken ct = default);
}
