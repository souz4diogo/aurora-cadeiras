using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Autenticacao;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aurora.Api.IntegrationTests;

/// <summary>
/// O rate limit do login é por IP. Atrás de um proxy o IP real só vem de X-Forwarded-For, que só pode ser
/// aceito de proxies confiáveis; do contrário qualquer cliente forjaria o IP para escapar do limite.
/// </summary>
[Collection("api")]
public class ForwardedHeadersTests(ApiFactory api)
{
    private const string LoginInvalido = "ninguem@teste.local";

    /// <summary>IP do proxy que, nos testes, "conecta" à API (o servidor em memória não tem IP remoto).</summary>
    private const string IpDoProxy = "198.51.100.7";

    private static Dictionary<string, string?> Configuracao(params (string Chave, string Valor)[] extras)
    {
        var configuracao = new Dictionary<string, string?> { ["RateLimit:LoginPorMinuto"] = "2" };
        foreach (var (chave, valor) in extras)
        {
            configuracao[chave] = valor;
        }

        return configuracao;
    }

    private HttpClient ClienteCom(Dictionary<string, string?> configuracao) =>
        api.WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(configuracao));
            b.ConfigureServices(servicos => servicos.AddTransient<IStartupFilter, DefinirIpRemotoFilter>());
        }).CreateClient();

    private static async Task<HttpStatusCode> TentarLoginAsync(HttpClient cliente, string ipEncaminhado)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(LoginInvalido, "senha-errada")),
        };
        requisicao.Headers.Add("X-Forwarded-For", ipEncaminhado);
        using var resposta = await cliente.SendAsync(requisicao);
        return resposta.StatusCode;
    }

    [Fact]
    public async Task Com_proxy_habilitado_o_limite_e_por_cliente_real()
    {
        using var cliente = ClienteCom(Configuracao(("ForwardedHeaders:Habilitado", "true")));

        Assert.Equal(HttpStatusCode.Unauthorized, await TentarLoginAsync(cliente, "203.0.113.10"));
        Assert.Equal(HttpStatusCode.Unauthorized, await TentarLoginAsync(cliente, "203.0.113.10"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await TentarLoginAsync(cliente, "203.0.113.10"));

        // Outro cliente real, atrás do mesmo proxy, não é afetado pelo limite do primeiro
        Assert.Equal(HttpStatusCode.Unauthorized, await TentarLoginAsync(cliente, "203.0.113.20"));
    }

    [Theory]
    [InlineData("ForwardedHeaders:ProxiesConfiaveis:0", IpDoProxy)]
    [InlineData("ForwardedHeaders:RedesConfiaveis:0", "198.51.100.0/24")]
    public async Task Proxy_na_lista_de_confiaveis_tem_o_cabecalho_aceito(string chave, string valor)
    {
        using var cliente = ClienteCom(Configuracao(("ForwardedHeaders:Habilitado", "true"), (chave, valor)));

        Assert.Equal(HttpStatusCode.Unauthorized, await TentarLoginAsync(cliente, "203.0.113.10"));
        Assert.Equal(HttpStatusCode.Unauthorized, await TentarLoginAsync(cliente, "203.0.113.10"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await TentarLoginAsync(cliente, "203.0.113.10"));
        Assert.Equal(HttpStatusCode.Unauthorized, await TentarLoginAsync(cliente, "203.0.113.20"));
    }

    [Fact]
    public async Task Sem_habilitar_o_cabecalho_e_ignorado_e_nao_serve_para_burlar_o_limite()
    {
        using var cliente = ClienteCom(Configuracao());

        Assert.Equal(HttpStatusCode.Unauthorized, await TentarLoginAsync(cliente, "203.0.113.1"));
        Assert.Equal(HttpStatusCode.Unauthorized, await TentarLoginAsync(cliente, "203.0.113.2"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await TentarLoginAsync(cliente, "203.0.113.3"));
    }

    [Fact]
    public async Task Proxy_fora_da_lista_de_confiaveis_tem_o_cabecalho_ignorado()
    {
        using var cliente = ClienteCom(Configuracao(
            ("ForwardedHeaders:Habilitado", "true"),
            ("ForwardedHeaders:ProxiesConfiaveis:0", "10.9.9.9")));

        Assert.Equal(HttpStatusCode.Unauthorized, await TentarLoginAsync(cliente, "203.0.113.1"));
        Assert.Equal(HttpStatusCode.Unauthorized, await TentarLoginAsync(cliente, "203.0.113.2"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await TentarLoginAsync(cliente, "203.0.113.3"));
    }

    private sealed class DefinirIpRemotoFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((contexto, proximo) =>
            {
                contexto.Connection.RemoteIpAddress = IPAddress.Parse(IpDoProxy);
                return proximo(contexto);
            });
            next(app);
        };
    }
}
