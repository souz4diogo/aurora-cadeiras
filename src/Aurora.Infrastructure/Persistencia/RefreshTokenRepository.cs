using Aurora.Application.Abstracoes;
using Aurora.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

public sealed class RefreshTokenRepository(AuroraDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> ObterPorHashAsync(string hash, CancellationToken ct = default) =>
        db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

    public async Task<bool> TentarRevogarAsync(long tokenId, DateTime agoraUtc, CancellationToken ct = default) =>
        await db.RefreshTokens
            .Where(t => t.Id == tokenId && t.RevogadoEm == null)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.RevogadoEm, agoraUtc), ct) == 1;

    public async Task RevogarTodosDoUsuarioAsync(int usuarioId, DateTime agoraUtc, CancellationToken ct = default)
    {
        var ativos = await db.RefreshTokens.Where(t => t.UsuarioId == usuarioId && t.RevogadoEm == null).ToListAsync(ct);
        foreach (var token in ativos)
        {
            token.Revogar(agoraUtc);
        }
    }

    public void Adicionar(RefreshToken token) => db.RefreshTokens.Add(token);
}
