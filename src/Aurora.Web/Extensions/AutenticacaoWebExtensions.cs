using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

namespace Aurora.Web.Extensions;

public static class AutenticacaoWebExtensions
{
    public const int InatividadePadraoEmMinutos = 30;

    /// <summary>
    /// Sessão por cookie (HttpOnly, SameSite=Strict, criptografado) que carrega os tokens da API. Fora do ambiente de
    /// desenvolvimento o cookie é sempre <c>Secure</c>, mesmo que a aplicação enxergue HTTP: atrás de um ingress que
    /// termina o TLS, a requisição chega como HTTP e o padrão "mesmo da requisição" emitiria o cookie sem a marca
    /// (docs/09 §6: HTTPS obrigatório). Para rodar o painel em HTTP puro fora do desenvolvimento (o docker compose
    /// local), desligue de forma explícita com <c>Sessao:CookieSomenteHttps=false</c>. Toda rota exige login, salvo
    /// [AllowAnonymous].
    /// </summary>
    public static IServiceCollection AddAuroraAutenticacaoWeb(
        this IServiceCollection services, IConfiguration configuracao, IHostEnvironment ambiente)
    {
        var somenteHttps = configuracao.GetValue("Sessao:CookieSomenteHttps", !ambiente.IsDevelopment());
        var inatividade = TimeSpan.FromMinutes(configuracao.GetValue("Sessao:InatividadeEmMinutos", InatividadePadraoEmMinutos));

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(opcoes =>
            {
                opcoes.Cookie.Name = "aurora.sessao";
                opcoes.Cookie.HttpOnly = true;
                opcoes.Cookie.SameSite = SameSiteMode.Strict;
                opcoes.Cookie.SecurePolicy = somenteHttps ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
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
