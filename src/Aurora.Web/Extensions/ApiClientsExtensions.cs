using Aurora.Web.Api.Auditoria;
using Aurora.Web.Api.Autenticacao;
using Aurora.Web.Api.Clientes;
using Aurora.Web.Api.Compras;
using Aurora.Web.Api.Comum;
using Aurora.Web.Api.Estacoes;
using Aurora.Web.Api.Estoque;
using Aurora.Web.Api.Expedicoes;
using Aurora.Web.Api.Fornecedores;
using Aurora.Web.Api.MateriasPrimas;
using Aurora.Web.Api.Painel;
using Aurora.Web.Api.Pedidos;
using Aurora.Web.Api.Producao;
using Aurora.Web.Api.Produtos;
using Aurora.Web.Api.Qualidade;
using Aurora.Web.Api.Relatorios;
using Aurora.Web.Api.Usuarios;

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
        services.AddModuloDaApi<IProdutosApi, ProdutosApi>(configuracao);
        services.AddModuloDaApi<IEstacoesApi, EstacoesApi>(configuracao);
        services.AddModuloDaApi<IPedidosApi, PedidosApi>(configuracao);
        services.AddModuloDaApi<IProducaoApi, ProducaoApi>(configuracao);
        services.AddModuloDaApi<IEstoqueApi, EstoqueApi>(configuracao);
        services.AddModuloDaApi<IComprasApi, ComprasApi>(configuracao);
        services.AddModuloDaApi<IExpedicoesApi, ExpedicoesApi>(configuracao);
        services.AddModuloDaApi<IPainelApi, PainelApi>(configuracao);
        services.AddModuloDaApi<IRelatoriosApi, RelatoriosApi>(configuracao);
        services.AddModuloDaApi<IQualidadeApi, QualidadeApi>(configuracao);
        services.AddModuloDaApi<IAuditoriaApi, AuditoriaApi>(configuracao);
        services.AddModuloDaApi<IUsuariosApi, UsuariosApi>(configuracao);
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
