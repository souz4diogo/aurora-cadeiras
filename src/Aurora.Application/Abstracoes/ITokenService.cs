using Aurora.Domain.Entidades;

namespace Aurora.Application.Abstracoes;

public sealed record TokenDeAcesso(string Valor, int ExpiraEmSegundos);

public interface ITokenService
{
    TokenDeAcesso GerarTokenDeAcesso(Usuario usuario);

    /// <summary>Token opaco aleatório; só o hash é guardado.</summary>
    string GerarRefreshToken();

    string CalcularHash(string refreshToken);
}
