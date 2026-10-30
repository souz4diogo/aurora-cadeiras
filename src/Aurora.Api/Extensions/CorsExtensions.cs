using Microsoft.AspNetCore.Cors.Infrastructure;

namespace Aurora.Api.Extensions;

public static class CorsExtensions
{
    /// <summary>Libera apenas as origens listadas em Cors:Origens; sem origens configuradas, nenhuma é liberada.</summary>
    public static IServiceCollection AddAuroraCors(this IServiceCollection services)
    {
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IConfiguration>((opcoes, configuracao) =>
            opcoes.AddDefaultPolicy(politica =>
            {
                var origens = configuracao.GetSection("Cors:Origens").Get<string[]>() ?? [];
                if (origens.Length > 0)
                {
                    politica.WithOrigins(origens).AllowAnyHeader().AllowAnyMethod();
                }
            }));
        return services;
    }
}
