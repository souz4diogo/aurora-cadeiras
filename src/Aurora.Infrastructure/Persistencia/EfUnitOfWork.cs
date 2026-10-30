using Aurora.Application.Abstracoes;

namespace Aurora.Infrastructure.Persistencia;

public sealed class EfUnitOfWork(AuroraDbContext db) : IUnitOfWork
{
    public Task<int> SalvarAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
