using Aurora.Application.Autenticacao;
using Aurora.Application.MateriasPrimas;
using Aurora.Application.Produtos;
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
        services.AddScoped<ProdutoService>();
        services.AddScoped<MateriaPrimaService>();
        return services;
    }
}
