using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Aurora.Web.Tests.Infraestrutura;

/// <summary>O painel web em memória, com o cliente HTTP da API apontando para a <see cref="ApiHost"/> (sem rede).</summary>
public sealed class PainelHost(ApiHost api) : WebApplicationFactory<PontoDeEntradaDaWeb>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Api:BaseUrl", "http://api.local");
        builder.ConfigureServices(servicos =>
            servicos.ConfigureHttpClientDefaults(cliente =>
                cliente.ConfigurePrimaryHttpMessageHandler(() => api.Server.CreateHandler())));
    }

    /// <summary>Um navegador novo: sem cookies, sem seguir redirecionamentos (os testes conferem cada resposta).</summary>
    public Navegador NovoNavegador() => new(CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
    }));
}
