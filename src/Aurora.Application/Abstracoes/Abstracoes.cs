using Aurora.Domain.Entidades;

namespace Aurora.Application.Abstracoes;

public interface IUnitOfWork
{
    Task<int> SalvarAsync(CancellationToken ct = default);
}

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken ct = default);

    Task<Usuario?> ObterPorIdAsync(int id, CancellationToken ct = default);

    Task<bool> EmailExisteAsync(string email, int? ignorarId, CancellationToken ct = default);

    Task<bool> ClienteExisteAsync(int clienteId, CancellationToken ct = default);

    Task<bool> ExisteAlgumAsync(CancellationToken ct = default);

    Task<(IReadOnlyList<Usuario> Itens, int Total)> ListarAsync(string? busca, int pagina, int tamanho, CancellationToken ct = default);

    void Adicionar(Usuario usuario);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> ObterPorHashAsync(string hash, CancellationToken ct = default);

    Task RevogarTodosDoUsuarioAsync(int usuarioId, DateTime agoraUtc, CancellationToken ct = default);

    void Adicionar(RefreshToken token);
}

public sealed record TokenDeAcesso(string Valor, int ExpiraEmSegundos);

public interface ITokenService
{
    TokenDeAcesso GerarTokenDeAcesso(Usuario usuario);

    /// <summary>Token opaco aleatório; só o hash é guardado.</summary>
    string GerarRefreshToken();

    string CalcularHash(string refreshToken);
}

public interface ISenhaHasher
{
    string Hash(string senha);

    bool Verificar(string hash, string senha);
}

public interface IGeradorDeSenha
{
    string GerarProvisoria();
}

public interface IRelogio
{
    DateTime AgoraUtc { get; }
}

public interface ICurrentUser
{
    int? UsuarioId { get; }
}
