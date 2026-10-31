using Aurora.Domain.Entidades;

namespace Aurora.Application.Abstracoes;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> ObterPorHashAsync(string hash, CancellationToken ct = default);

    /// <summary>
    /// Revoga o token de forma atômica (só se ainda estiver ativo) e devolve se esta chamada foi quem o revogou.
    /// Em requisições simultâneas com o mesmo token, apenas uma recebe <c>true</c>.
    /// </summary>
    Task<bool> TentarRevogarAsync(long tokenId, DateTime agoraUtc, CancellationToken ct = default);

    Task RevogarTodosDoUsuarioAsync(int usuarioId, DateTime agoraUtc, CancellationToken ct = default);

    void Adicionar(RefreshToken token);
}
