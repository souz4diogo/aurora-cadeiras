using Aurora.Application.Autenticacao;
using Aurora.Application.Clientes;
using Aurora.Application.Estacoes;
using Aurora.Application.FichasTecnicas;
using Aurora.Application.Fornecedores;
using Aurora.Application.MateriasPrimas;
using Aurora.Application.Pedidos;
using Aurora.Application.Produtos;
using Aurora.Application.Roteiros;
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
        services.AddScoped<ClienteService>();
        services.AddScoped<FornecedorService>();
        services.AddScoped<EstacaoService>();
        services.AddScoped<FichaTecnicaService>();
        services.AddScoped<RoteiroService>();
        services.AddScoped<PedidoService>();
        services.AddScoped<MateriaPrimaService>();
        return services;
    }
}
