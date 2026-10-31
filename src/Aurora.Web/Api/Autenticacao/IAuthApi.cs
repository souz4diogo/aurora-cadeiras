namespace Aurora.Web.Api.Autenticacao;

/// <summary>Cliente dos endpoints de autenticação, que não passam pelo handler de token (eles é que o alimentam).</summary>
public interface IAuthApi
{
    Task<TokenResposta> LoginAsync(string email, string senha, CancellationToken ct = default);

    Task<TokenResposta> RenovarAsync(string refreshToken, CancellationToken ct = default);

    Task SairAsync(string accessToken, string refreshToken, CancellationToken ct = default);
}
