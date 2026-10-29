using Microsoft.OpenApi;

namespace Aurora.Api.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddAuroraSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(opcoes =>
        {
            opcoes.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Aurora Cadeiras API",
                Version = "v1",
                Description = "API REST da fábrica Aurora Cadeiras (pedido, produção, estoque, qualidade e expedição).",
            });
            opcoes.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Informe o token de acesso obtido em POST /api/v1/auth/login.",
            });
            opcoes.AddSecurityRequirement(documento => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", documento)] = [],
            });
        });
        return services;
    }
}
