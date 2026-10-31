using Aurora.Application.Abstracoes;
using Aurora.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

public sealed class FichaTecnicaRepository(AuroraDbContext db) : IFichaTecnicaRepository
{
    public async Task<IReadOnlyList<FichaTecnicaItem>> ListarPorProdutoAsync(int produtoId, CancellationToken ct = default) =>
        await db.FichasTecnicas.AsNoTracking().Where(i => i.ProdutoId == produtoId).ToListAsync(ct);

    public Task SubstituirAsync(int produtoId, IReadOnlyList<FichaTecnicaItem> itens, CancellationToken ct = default) =>
        db.ExecutarEmTransacaoAsync(async () =>
        {
            await db.FichasTecnicas.Where(i => i.ProdutoId == produtoId).ExecuteDeleteAsync(ct);
            db.FichasTecnicas.AddRange(itens);
            await db.SaveChangesAsync(ct);
        }, ct);
}
