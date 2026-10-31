using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Aurora.Application.Autenticacao;
using Microsoft.Extensions.Configuration;

namespace Aurora.Api.IntegrationTests;

[Collection("api")]
public class AutenticacaoTests(ApiFactory api)
{
    [Fact]
    public async Task Login_valido_devolve_token_de_acesso_e_refresh_token()
    {
        using var cliente = api.CreateClient();

        var resposta = await cliente.LoginAsync(ApiFactory.EmailAdmin, ApiFactory.SenhaAdmin);

        resposta.AssertStatus(HttpStatusCode.OK);
        var token = await resposta.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(token.RefreshToken));
        Assert.Equal(1800, token.ExpiresInSeconds);
        Assert.Equal("Administrador", token.Usuario.Perfil);
    }

    [Fact]
    public async Task Login_com_credencial_invalida_nao_revela_qual_campo_falhou()
    {
        using var cliente = api.CreateClient();

        var senhaErrada = await cliente.LoginAsync(ApiFactory.EmailAdmin, "senha-errada-123");
        var emailInexistente = await cliente.LoginAsync("ninguem@teste.local", "senha-errada-123");

        senhaErrada.AssertStatus(HttpStatusCode.Unauthorized);
        emailInexistente.AssertStatus(HttpStatusCode.Unauthorized);
        var a = await senhaErrada.ProblemaAsync();
        var b = await emailInexistente.ProblemaAsync();
        Assert.Equal("CREDENCIAIS_INVALIDAS", a.GetProperty("code").GetString());
        Assert.Equal(a.GetProperty("detail").GetString(), b.GetProperty("detail").GetString());
        Assert.Equal(a.GetProperty("code").GetString(), b.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Login_com_corpo_invalido_devolve_400_com_erros_por_campo()
    {
        using var cliente = api.CreateClient();

        var resposta = await cliente.LoginAsync("", "");

        resposta.AssertStatus(HttpStatusCode.BadRequest);
        var problema = await resposta.ProblemaAsync();
        Assert.Equal("VALIDACAO", problema.GetProperty("code").GetString());
        Assert.True(problema.GetProperty("errors").TryGetProperty("Email", out _));
        Assert.True(problema.GetProperty("errors").TryGetProperty("Senha", out _));
    }

    [Fact]
    public async Task Cinco_falhas_bloqueiam_a_conta_mesmo_com_a_senha_correta()
    {
        var (usuario, senha) = await api.CriarUsuarioAsync("Vendedor");
        using var cliente = api.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            (await cliente.LoginAsync(usuario.Email, "senha-errada-123")).AssertStatus(HttpStatusCode.Unauthorized);
        }

        var bloqueado = await cliente.LoginAsync(usuario.Email, senha);

        bloqueado.AssertStatus(HttpStatusCode.Unauthorized);
        Assert.Equal("CONTA_BLOQUEADA", await bloqueado.CodigoAsync());
    }

    [Fact]
    public async Task Me_devolve_o_usuario_autenticado()
    {
        using var cliente = await api.ComoAsync("Vendedor");

        var resposta = await cliente.GetAsync("/api/v1/auth/me");

        resposta.AssertStatus(HttpStatusCode.OK);
        var me = await resposta.Content.ReadFromJsonAsync<MeDto>();
        Assert.Equal("Vendedor", me!.Usuario.Perfil);
    }

    [Fact]
    public async Task Rota_protegida_sem_token_devolve_401_em_problem_json()
    {
        using var cliente = api.CreateClient();

        var resposta = await cliente.GetAsync("/api/v1/auth/me");

        resposta.AssertStatus(HttpStatusCode.Unauthorized);
        Assert.Equal("NAO_AUTENTICADO", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Token_adulterado_e_rejeitado()
    {
        var token = await api.LoginAsync(ApiFactory.EmailAdmin, ApiFactory.SenhaAdmin);
        using var cliente = api.CreateClient();
        var partes = token.AccessToken.Split('.');
        var adulterado = $"{partes[0]}.{partes[1]}.{new string(partes[2].Reverse().ToArray())}";
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adulterado);

        var resposta = await cliente.GetAsync("/api/v1/auth/me");

        resposta.AssertStatus(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_e_rotativo_e_o_reuso_derruba_a_sessao()
    {
        var (usuario, senha) = await api.CriarUsuarioAsync("Operador");
        var primeiro = await api.LoginAsync(usuario.Email, senha);
        using var cliente = api.CreateClient();

        var renovado = await cliente.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(primeiro.RefreshToken));
        renovado.AssertStatus(HttpStatusCode.OK);
        var segundo = (await renovado.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.NotEqual(primeiro.RefreshToken, segundo.RefreshToken);

        // O token anterior foi revogado; reutilizá-lo é tratado como roubo e revoga também o novo
        var reuso = await cliente.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(primeiro.RefreshToken));
        reuso.AssertStatus(HttpStatusCode.Unauthorized);
        Assert.Equal("REFRESH_INVALIDO", await reuso.CodigoAsync());

        var novoRevogado = await cliente.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(segundo.RefreshToken));
        novoRevogado.AssertStatus(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_simultaneo_com_o_mesmo_token_so_tem_um_vencedor()
    {
        const int tentativas = 8;
        var (usuario, senha) = await api.CriarUsuarioAsync("Operador");
        var token = await api.LoginAsync(usuario.Email, senha);
        using var cliente = api.CreateClient();

        var respostas = await Task.WhenAll(Enumerable.Range(0, tentativas).Select(_ =>
            cliente.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(token.RefreshToken))));

        Assert.Single(respostas, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal(tentativas - 1, respostas.Count(r => r.StatusCode == HttpStatusCode.Unauthorized));
    }

    [Fact]
    public async Task Logout_revoga_o_refresh_token()
    {
        var (usuario, senha) = await api.CriarUsuarioAsync("Operador");
        var token = await api.LoginAsync(usuario.Email, senha);
        using var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var logout = await cliente.PostAsJsonAsync("/api/v1/auth/logout", new LogoutRequest(token.RefreshToken));
        var refresh = await cliente.PostAsJsonAsync("/api/v1/auth/refresh", new RefreshRequest(token.RefreshToken));

        logout.AssertStatus(HttpStatusCode.NoContent);
        refresh.AssertStatus(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Excesso_de_tentativas_de_login_devolve_429()
    {
        using var limitada = api.WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, c) =>
            c.AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimit:LoginPorMinuto"] = "3" })));
        using var cliente = limitada.CreateClient();

        var codigos = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            codigos.Add((await cliente.LoginAsync("ninguem@teste.local", "senha-errada-123")).StatusCode);
        }

        Assert.Equal(3, codigos.Count(c => c == HttpStatusCode.Unauthorized));
        Assert.Equal(2, codigos.Count(c => c == HttpStatusCode.TooManyRequests));
    }
}
