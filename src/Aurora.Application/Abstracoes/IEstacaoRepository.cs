using Aurora.Domain.Entidades;

namespace Aurora.Application.Abstracoes;

public interface IEstacaoRepository
{
    Task<IReadOnlyList<EstacaoProducao>> ListarAsync(CancellationToken ct = default);
}
