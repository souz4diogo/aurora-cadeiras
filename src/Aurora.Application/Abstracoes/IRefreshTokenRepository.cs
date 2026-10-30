using Aurora.Domain.Entidades;

namespace Aurora.Application.Abstracoes;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> ObterPorHashAsync(string hash, CancellationToken ct = default);

    Task RevogarTodosDoUsuarioAsync(int usuarioId, DateTime agoraUtc, CancellationToken ct = default);

    void Adicionar(RefreshToken token);
}
