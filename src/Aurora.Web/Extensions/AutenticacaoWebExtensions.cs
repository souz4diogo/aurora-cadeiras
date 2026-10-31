using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace Aurora.Web.Extensions;

public static class AutenticacaoWebExtensions
{
    public const int InatividadePadraoEmMinutos = 30;

    /// <summary>
    /// Sessão por cookie (HttpOnly, SameSite=Strict, criptografado) que carrega os tokens da API. O cookie só é
    /// marcado Secure quando a requisição é HTTPS, para o ambiente local em HTTP continuar funcionando; atrás de um
    /// ingress com TLS, a requisição chega como HTTPS e o cookie sai Secure. Toda rota exige login, salvo [AllowAnonymous].
    /// </summary>
    public static IServiceCollection AddAuroraAutenticacaoWeb(this IServiceCollection services, IConfiguration configuracao)
    {
        var inatividade = TimeSpan.FromMinutes(configuracao.GetValue("Sessao:InatividadeEmMinutos", InatividadePadraoEmMinutos));

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(opcoes =>
            {
                opcoes.Cookie.Name = "aurora.sessao";
                opcoes.Cookie.HttpOnly = true;
                opcoes.Cookie.SameSite = SameSiteMode.Strict;
                opcoes.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                opcoes.LoginPath = "/Conta/Login";
                opcoes.AccessDeniedPath = "/Conta/AcessoNegado";
                opcoes.ExpireTimeSpan = inatividade;
                opcoes.SlidingExpiration = true;
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        return services;
    }
}
