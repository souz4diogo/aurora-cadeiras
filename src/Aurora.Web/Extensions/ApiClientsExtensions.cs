using Aurora.Web.Api.Autenticacao;
using Aurora.Web.Api.Clientes;
using Aurora.Web.Api.Comum;
using Aurora.Web.Api.Fornecedores;
using Aurora.Web.Api.MateriasPrimas;

namespace Aurora.Web.Extensions;

public static class ApiClientsExtensions
{
    /// <summary>
    /// Registra um cliente tipado por módulo da API. Todos, menos o de autenticação, passam pelo
    /// <see cref="TokenHandler"/>, que anexa e renova o token do usuário logado.
    /// </summary>
    public static IServiceCollection AddAuroraApi(this IServiceCollection services, IConfiguration configuracao)
    {
        var endereco = EnderecoDaApi(configuracao);

        services.AddHttpContextAccessor();
        services.AddSingleton(TimeProvider.System);
        services.AddTransient<TokenHandler>();

        services.AddHttpClient<IAuthApi, AuthApi>(cliente => cliente.BaseAddress = endereco);

        services.AddModuloDaApi<IClientesApi, ClientesApi>(configuracao);
        services.AddModuloDaApi<IFornecedoresApi, FornecedoresApi>(configuracao);
        services.AddModuloDaApi<IMateriasPrimasApi, MateriasPrimasApi>(configuracao);
        return services;
    }

    /// <summary>Cliente tipado de um módulo, com o endereço da API e o handler de token.</summary>
    internal static IHttpClientBuilder AddModuloDaApi<TInterface, TImplementacao>(
        this IServiceCollection services, IConfiguration configuracao)
        where TInterface : class
        where TImplementacao : class, TInterface
    {
        var endereco = EnderecoDaApi(configuracao);
        return services.AddHttpClient<TInterface, TImplementacao>(cliente => cliente.BaseAddress = endereco)
            .AddHttpMessageHandler<TokenHandler>();
    }

    private static Uri EnderecoDaApi(IConfiguration configuracao)
    {
        var enderecoBase = configuracao["Api:BaseUrl"];
        if (string.IsNullOrWhiteSpace(enderecoBase))
        {
            throw new InvalidOperationException("Api:BaseUrl não configurada. Defina a variável de ambiente Api__BaseUrl.");
        }

        // Com a barra final, as rotas relativas ("api/v1/...") se somam ao endereço em vez de substituí-lo
        return new Uri(enderecoBase.TrimEnd('/') + "/", UriKind.Absolute);
    }
}
