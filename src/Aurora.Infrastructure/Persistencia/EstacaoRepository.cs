using Aurora.Application.Abstracoes;
using Aurora.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

public sealed class EstacaoRepository(AuroraDbContext db) : IEstacaoRepository
{
    public async Task<IReadOnlyList<EstacaoProducao>> ListarAsync(CancellationToken ct = default) =>
        await db.Estacoes.AsNoTracking().OrderBy(e => e.Ordem).ToListAsync(ct);
}
