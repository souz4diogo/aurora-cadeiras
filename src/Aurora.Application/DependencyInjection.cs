using Aurora.Application.Autenticacao;
using Aurora.Application.Usuarios;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Aurora.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAuroraApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
        services.AddScoped<AuthService>();
        services.AddScoped<UsuarioService>();
        return services;
    }
}
