using System.Globalization;
using System.Security.Claims;
using Aurora.Web.Api.Autenticacao;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Aurora.Web.Seguranca;

public sealed record TokensDaSessao(string AccessToken, string RefreshToken, DateTimeOffset ExpiraEm);

/// <summary>
/// Guarda os tokens da API dentro do cookie de autenticação (criptografado e HttpOnly) e os lê de volta.
/// Os tokens renovados ficam também em <c>HttpContext.Items</c>: o cookie da requisição continua com os valores
/// antigos até a resposta, e reusar um refresh token já trocado derrubaria a sessão (reuso detectado pela API).
/// </summary>
public static class SessaoDeTokens
{
    private const string ChaveAccessToken = "access_token";
    private const string ChaveRefreshToken = "refresh_token";
    private const string ChaveExpiraEm = "expires_at";
    private const string ChaveNaRequisicao = "aurora.tokens";

    public static async Task<TokensDaSessao?> LerAsync(HttpContext contexto)
    {
        if (contexto.Items[ChaveNaRequisicao] is TokensDaSessao daRequisicao)
        {
            return daRequisicao;
        }

        var autenticacao = await contexto.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!autenticacao.Succeeded)
        {
            return null;
        }

        var access = autenticacao.Properties?.GetTokenValue(ChaveAccessToken);
        var refresh = autenticacao.Properties?.GetTokenValue(ChaveRefreshToken);
        var expiraTexto = autenticacao.Properties?.GetTokenValue(ChaveExpiraEm);
        if (access is null || refresh is null
            || !DateTimeOffset.TryParse(expiraTexto, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiraEm))
        {
            return null;
        }

        var tokens = new TokensDaSessao(access, refresh, expiraEm);
        contexto.Items[ChaveNaRequisicao] = tokens;
        return tokens;
    }

    /// <summary>Abre a sessão do usuário (ou atualiza os tokens dela) gravando o cookie na resposta.</summary>
    public static async Task GravarAsync(HttpContext contexto, ClaimsPrincipal usuario, TokenResposta resposta, DateTimeOffset agora)
    {
        var expiraEm = agora.AddSeconds(resposta.ExpiresInSeconds);
        var propriedades = new AuthenticationProperties { IsPersistent = false };
        propriedades.StoreTokens(
        [
            new AuthenticationToken { Name = ChaveAccessToken, Value = resposta.AccessToken },
            new AuthenticationToken { Name = ChaveRefreshToken, Value = resposta.RefreshToken },
            new AuthenticationToken { Name = ChaveExpiraEm, Value = expiraEm.ToString("o", CultureInfo.InvariantCulture) },
        ]);

        contexto.Items[ChaveNaRequisicao] = new TokensDaSessao(resposta.AccessToken, resposta.RefreshToken, expiraEm);
        await contexto.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, usuario, propriedades);
    }

    public static Task EncerrarAsync(HttpContext contexto)
    {
        contexto.Items.Remove(ChaveNaRequisicao);
        return contexto.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    /// <summary>Identidade da web a partir do usuário devolvido pela API: nome, perfil (papel) e id.</summary>
    public static ClaimsPrincipal CriarUsuario(UsuarioResumoResposta usuario)
    {
        var identidade = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, usuario.Nome),
                new Claim(ClaimTypes.Role, usuario.Perfil),
            ],
            CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identidade);
    }
}
