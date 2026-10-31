using System.Net;
using System.Text;
using Aurora.Web.Api.Autenticacao;
using Aurora.Web.Api.Comum;
using Aurora.Web.Seguranca;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Aurora.Web.Tests;

public sealed class TokenHandlerTests : IDisposable
{
    private static readonly DateTimeOffset Agora = new(2026, 10, 30, 15, 0, 0, TimeSpan.Zero);
    private static readonly UsuarioResumoResposta Maria = new(7, "Maria", "Vendedor");

    private readonly Mock<IAuthApi> _auth = new();
    private readonly ApiFalsa _api = new();

    public void Dispose() => _api.Dispose();

    private static TokenResposta Tokens(string sufixo, int expiraEmSegundos = 1800) =>
        new($"acesso-{sufixo}", expiraEmSegundos, $"refresh-{sufixo}", Maria);

    /// <summary>Contexto de uma requisição com a sessão aberta (cookie assinado com os tokens iniciais).</summary>
    private static async Task<DefaultHttpContext> ContextoLogadoAsync(TokenResposta tokens)
    {
        var servicos = new ServiceCollection();
        servicos.AddLogging();
        servicos.AddDataProtection();
        servicos.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
        var contexto = new DefaultHttpContext { RequestServices = servicos.BuildServiceProvider() };
        await SessaoDeTokens.GravarAsync(contexto, SessaoDeTokens.CriarUsuario(tokens.Usuario), tokens, Agora);
        return contexto;
    }

    private HttpClient Cliente(HttpContext? contexto)
    {
        var handler = new TokenHandler(new HttpContextAccessor { HttpContext = contexto }, _auth.Object, new RelogioFixo(Agora))
        {
            InnerHandler = _api,
        };
        return new HttpClient(handler) { BaseAddress = new Uri("http://api.local/") };
    }

    private static ApiException RefreshRecusado() =>
        new(HttpStatusCode.Unauthorized, "REFRESH_INVALIDO", "Sessão inválida", "Sessão inválida ou expirada.", new Dictionary<string, string[]>());

    [Fact]
    public async Task Anexa_o_bearer_do_usuario_logado()
    {
        var contexto = await ContextoLogadoAsync(Tokens("1"));
        _api.Responder(HttpStatusCode.OK);

        using var resposta = await Cliente(contexto).GetAsync("api/v1/clientes");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Bearer acesso-1", _api.Recebidas.Single().Autorizacao);
        _auth.Verify(a => a.RenovarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Ao_receber_401_renova_o_token_e_repete_a_requisicao_com_o_mesmo_corpo()
    {
        var contexto = await ContextoLogadoAsync(Tokens("1"));
        _auth.Setup(a => a.RenovarAsync("refresh-1", It.IsAny<CancellationToken>())).ReturnsAsync(Tokens("2"));
        _api.Responder(HttpStatusCode.Unauthorized);
        _api.Responder(HttpStatusCode.Created);

        using var resposta = await Cliente(contexto).PostAsync("api/v1/clientes", new StringContent("{\"nome\":\"Lima\"}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Equal(["Bearer acesso-1", "Bearer acesso-2"], _api.Recebidas.Select(r => r.Autorizacao));
        Assert.Equal(["{\"nome\":\"Lima\"}", "{\"nome\":\"Lima\"}"], _api.Recebidas.Select(r => r.Corpo));
        var sessao = await SessaoDeTokens.LerAsync(contexto);
        Assert.Equal("acesso-2", sessao!.AccessToken);
        Assert.Equal("refresh-2", sessao.RefreshToken);
    }

    [Fact]
    public async Task Depois_de_renovar_as_proximas_chamadas_da_requisicao_usam_o_token_novo_sem_renovar_de_novo()
    {
        var contexto = await ContextoLogadoAsync(Tokens("1"));
        _auth.Setup(a => a.RenovarAsync("refresh-1", It.IsAny<CancellationToken>())).ReturnsAsync(Tokens("2"));
        _api.Responder(HttpStatusCode.Unauthorized);
        _api.Responder(HttpStatusCode.OK);
        _api.Responder(HttpStatusCode.OK);
        var cliente = Cliente(contexto);

        (await cliente.GetAsync("api/v1/produtos")).Dispose();
        (await cliente.GetAsync("api/v1/pedidos")).Dispose();

        Assert.Equal("Bearer acesso-2", _api.Recebidas[^1].Autorizacao);
        _auth.Verify(a => a.RenovarAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Renova_antes_de_expirar_sem_esperar_o_401()
    {
        var contexto = await ContextoLogadoAsync(Tokens("1", expiraEmSegundos: 10));
        _auth.Setup(a => a.RenovarAsync("refresh-1", It.IsAny<CancellationToken>())).ReturnsAsync(Tokens("2"));
        _api.Responder(HttpStatusCode.OK);

        using var resposta = await Cliente(contexto).GetAsync("api/v1/clientes");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Bearer acesso-2", _api.Recebidas.Single().Autorizacao);
    }

    [Fact]
    public async Task Se_a_renovacao_falhar_lanca_sessao_expirada_e_nao_repete_a_requisicao()
    {
        var contexto = await ContextoLogadoAsync(Tokens("1"));
        _auth.Setup(a => a.RenovarAsync("refresh-1", It.IsAny<CancellationToken>())).ThrowsAsync(RefreshRecusado());
        _api.Responder(HttpStatusCode.Unauthorized);

        await Assert.ThrowsAsync<SessaoExpiradaException>(() => Cliente(contexto).GetAsync("api/v1/clientes"));

        Assert.Single(_api.Recebidas);
    }

    [Fact]
    public async Task Sem_sessao_lanca_sessao_expirada_sem_chamar_a_api()
    {
        var contexto = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddLogging().AddDataProtection().Services
                .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie().Services
                .BuildServiceProvider(),
        };

        await Assert.ThrowsAsync<SessaoExpiradaException>(() => Cliente(contexto).GetAsync("api/v1/clientes"));

        Assert.Empty(_api.Recebidas);
    }

    [Fact]
    public async Task Chamada_fora_de_uma_requisicao_web_e_um_erro_de_programacao()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Cliente(contexto: null).GetAsync("api/v1/clientes"));
    }

    [Fact]
    public async Task Se_a_api_continuar_negando_depois_de_renovar_devolve_o_401()
    {
        var contexto = await ContextoLogadoAsync(Tokens("1"));
        _auth.Setup(a => a.RenovarAsync("refresh-1", It.IsAny<CancellationToken>())).ReturnsAsync(Tokens("2"));
        _api.Responder(HttpStatusCode.Unauthorized);
        _api.Responder(HttpStatusCode.Unauthorized);

        using var resposta = await Cliente(contexto).GetAsync("api/v1/clientes");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal(2, _api.Recebidas.Count);
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }

    /// <summary>API simulada: responde na ordem programada e guarda o que recebeu (inclusive o corpo, que só pode ser lido uma vez).</summary>
    private sealed class ApiFalsa : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _respostas = new();

        public List<(string? Autorizacao, string? Corpo)> Recebidas { get; } = [];

        public void Responder(HttpStatusCode status) => _respostas.Enqueue(status);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var corpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Recebidas.Add((request.Headers.Authorization?.ToString(), corpo));
            return new HttpResponseMessage(_respostas.Dequeue()) { Content = new StringContent("{}") };
        }
    }
}
