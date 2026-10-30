using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Autenticacao;

public sealed record UsuarioResumoResposta(int Id, string Nome, string Perfil);

public sealed record TokenResposta(string AccessToken, int ExpiresInSeconds, string RefreshToken, UsuarioResumoResposta Usuario);

/// <summary>Cliente dos endpoints de autenticação, que não passam pelo handler de token (eles é que o alimentam).</summary>
public interface IAuthApi
{
    Task<TokenResposta> LoginAsync(string email, string senha, CancellationToken ct = default);

    Task<TokenResposta> RenovarAsync(string refreshToken, CancellationToken ct = default);

    Task SairAsync(string accessToken, string refreshToken, CancellationToken ct = default);
}

public sealed class AuthApi(HttpClient http) : ApiClientBase(http), IAuthApi
{
    public Task<TokenResposta> LoginAsync(string email, string senha, CancellationToken ct = default) =>
        HttpPostAsync<TokenResposta>("api/v1/auth/login", new { Email = email, Senha = senha }, ct);

    public Task<TokenResposta> RenovarAsync(string refreshToken, CancellationToken ct = default) =>
        HttpPostAsync<TokenResposta>("api/v1/auth/refresh", new { RefreshToken = refreshToken }, ct);

    public Task SairAsync(string accessToken, string refreshToken, CancellationToken ct = default) =>
        EnviarSemRespostaAsync(HttpMethod.Post, "api/v1/auth/logout", new { RefreshToken = refreshToken }, accessToken, ct);
}
