using Aurora.Infrastructure.Persistencia;

namespace Aurora.Api.Extensions;

public static class HealthExtensions
{
    public static IServiceCollection AddAuroraHealth(this IServiceCollection services)
    {
        services.AddHealthChecks().AddDbContextCheck<AuroraDbContext>("banco", tags: ["ready"]);
        return services;
    }

    public static IEndpointRouteBuilder MapAuroraHealth(this IEndpointRouteBuilder endpoints)
    {
        // /health: o processo está vivo (sem consultar o banco). /health/ready: inclui o banco.
        endpoints.MapHealthChecks("/health", new() { Predicate = _ => false }).AllowAnonymous();
        endpoints.MapHealthChecks("/health/ready", new() { Predicate = r => r.Tags.Contains("ready") }).AllowAnonymous();
        return endpoints;
    }
}
