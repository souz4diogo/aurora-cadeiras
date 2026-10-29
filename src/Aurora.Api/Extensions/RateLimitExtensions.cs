using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Aurora.Api.Extensions;

public static class RateLimitExtensions
{
    public const string PoliticaDeLogin = "login";

    public static IServiceCollection AddAuroraRateLimit(this IServiceCollection services)
    {
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IConfiguration>((opcoes, configuracao) =>
        {
            var limite = configuracao.GetValue("RateLimit:LoginPorMinuto", 10);
            opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opcoes.AddPolicy(PoliticaDeLogin, contexto => RateLimitPartition.GetFixedWindowLimiter(
                contexto.Connection.RemoteIpAddress?.ToString() ?? "desconhecido",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limite,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });
        return services;
    }
}

