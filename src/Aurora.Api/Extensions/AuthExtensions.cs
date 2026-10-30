using System.Text;
using Aurora.Infrastructure.Seguranca;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Aurora.Api.Extensions;

public static class AuthExtensions
{
    public static IServiceCollection AddAuroraAuth(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Lido de IOptions<JwtOptions> na primeira requisição (a chave vem de variável de ambiente/Key Vault)
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
            {
                bearer.MapInboundClaims = false;
                bearer.RequireHttpsMetadata = true;
                bearer.TokenValidationParameters = CriarParametrosDeValidacao(jwt.Value);
            });

        // Seguro por padrão: toda rota exige autenticação, salvo [AllowAnonymous]
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        return services;
    }

    private static TokenValidationParameters CriarParametrosDeValidacao(JwtOptions jwt) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = NomesDeClaims.Nome,
        RoleClaimType = NomesDeClaims.Perfil,
    };
}
