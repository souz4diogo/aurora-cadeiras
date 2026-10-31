using Aurora.Application.Abstracoes;
using Aurora.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

public sealed class RoteiroRepository(AuroraDbContext db) : IRoteiroRepository
{
    public async Task<IReadOnlyList<RoteiroProduto>> ListarPorProdutoAsync(int produtoId, CancellationToken ct = default) =>
        await db.Roteiros.AsNoTracking().Where(r => r.ProdutoId == produtoId).OrderBy(r => r.Sequencia).ToListAsync(ct);

    public Task SubstituirAsync(int produtoId, IReadOnlyList<RoteiroProduto> etapas, CancellationToken ct = default) =>
        db.ExecutarEmTransacaoAsync(async () =>
        {
            await db.Roteiros.Where(r => r.ProdutoId == produtoId).ExecuteDeleteAsync(ct);
            db.Roteiros.AddRange(etapas);
            await db.SaveChangesAsync(ct);
        }, ct);
}
