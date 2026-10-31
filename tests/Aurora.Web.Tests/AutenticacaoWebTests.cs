using System.Net;
using Aurora.Web.Tests.Infraestrutura;

namespace Aurora.Web.Tests;

[Collection("painel")]
public class AutenticacaoWebTests(CenarioDoPainel cenario)
{
    private static string CookieDeSessao(HttpResponseMessage resposta) =>
        resposta.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.FirstOrDefault(c => c.StartsWith("aurora.sessao=", StringComparison.Ordinal)) ?? ""
            : "";

    [Fact]
    public async Task Visitante_e_levado_ao_login_e_a_pagina_pedida_fica_guardada()
    {
        using var navegador = cenario.Painel.NovoNavegador();

        var inicio = await navegador.GetAsync("/");
        var protegida = await navegador.GetAsync("/Home/Index?x=1");

        Assert.Equal(HttpStatusCode.Redirect, inicio.StatusCode);
        Assert.StartsWith("/Conta/Login?ReturnUrl=", Navegador.Destino(inicio), StringComparison.Ordinal);
        Assert.Contains("ReturnUrl=%2FHome%2FIndex%3Fx%3D1", Navegador.Destino(protegida), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pagina_de_login_e_acessivel_com_idioma_rotulos_e_link_para_pular_ao_conteudo()
    {
        using var navegador = cenario.Painel.NovoNavegador();

        var html = Navegador.Decodificar(await navegador.PaginaAsync("/Conta/Login"));

        Assert.Contains("<html lang=\"pt-BR\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"#conteudo\"", html, StringComparison.Ordinal);
        Assert.Contains("Pular para o conteúdo", html, StringComparison.Ordinal);
        Assert.Matches("<label[^>]*for=\"Email\"", html);
        Assert.Matches("<label[^>]*for=\"Senha\"", html);
        Assert.Matches("<main[^>]*id=\"conteudo\"", html);
        Assert.Contains("autocomplete=\"current-password\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Senha_errada_mostra_mensagem_generica_e_nao_abre_sessao()
    {
        using var navegador = cenario.Painel.NovoNavegador();

        var resposta = await navegador.EntrarAsync(ApiHost.EmailAdmin, "senha-incorreta");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("E-mail ou senha inválidos.", Navegador.Decodificar(await resposta.Content.ReadAsStringAsync()), StringComparison.Ordinal);
        Assert.Equal("", CookieDeSessao(resposta));
        Assert.Equal(HttpStatusCode.Redirect, (await navegador.GetAsync("/")).StatusCode);
    }

    [Fact]
    public async Task Email_inexistente_recebe_a_mesma_mensagem_da_senha_errada()
    {
        using var navegador = cenario.Painel.NovoNavegador();

        var resposta = await navegador.EntrarAsync("ninguem@teste.local", "qualquer-senha");

        Assert.Contains("E-mail ou senha inválidos.", Navegador.Decodificar(await resposta.Content.ReadAsStringAsync()), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Campos_em_branco_sao_apontados_sem_chamar_a_api()
    {
        using var navegador = cenario.Painel.NovoNavegador();

        var resposta = await navegador.EntrarAsync("", "");

        var html = Navegador.Decodificar(await resposta.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("Informe o e-mail.", html, StringComparison.Ordinal);
        Assert.Contains("Informe a senha.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_correto_abre_sessao_em_cookie_http_only_same_site_strict_e_mostra_o_usuario()
    {
        using var navegador = cenario.Painel.NovoNavegador();

        var resposta = await navegador.EntrarAsync(ApiHost.EmailAdmin, ApiHost.SenhaAdmin);

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal("/", Navegador.Destino(resposta));
        var cookie = CookieDeSessao(resposta);
        Assert.NotEqual("", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("eyJ", cookie, StringComparison.Ordinal); // o JWT não aparece em claro no cookie

        var inicio = Navegador.Decodificar(await navegador.PaginaAsync("/"));
        Assert.Contains("Olá, Administrador", inicio, StringComparison.Ordinal);
        Assert.Contains("(Administrador)", inicio, StringComparison.Ordinal);
        Assert.Contains(">Sair</button>", inicio, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Administrador", new[] { "Clientes", "Fornecedores", "Produtos", "Matérias-primas", "Pedidos", "Produção", "Estoque" })]
    [InlineData("GerenteProducao", new[] { "Produtos", "Pedidos", "Produção", "Estoque" })]
    [InlineData("Vendedor", new[] { "Clientes", "Pedidos" })]
    [InlineData("Almoxarife", new[] { "Fornecedores", "Matérias-primas", "Estoque" })]
    public async Task Menu_mostra_so_as_areas_do_perfil(string perfil, string[] esperados)
    {
        using var navegador = await cenario.EntrarComoAsync(perfil);
        var todos = new[] { "Clientes", "Fornecedores", "Produtos", "Matérias-primas", "Pedidos", "Produção", "Estoque" };

        var inicio = Navegador.Decodificar(await navegador.PaginaAsync("/"));

        foreach (var titulo in todos)
        {
            var presente = inicio.Contains($">{titulo}</a>", StringComparison.Ordinal);
            Assert.Equal(esperados.Contains(titulo), presente);
        }
    }

    [Theory]
    [InlineData("Operador")]
    [InlineData("Inspetor")]
    public async Task Operador_e_inspetor_veem_o_aviso_do_aplicativo_mobile_e_nenhum_menu(string perfil)
    {
        using var navegador = await cenario.EntrarComoAsync(perfil);

        var inicio = Navegador.Decodificar(await navegador.PaginaAsync("/"));

        Assert.Contains("aplicativo mobile", inicio, StringComparison.Ordinal);
        Assert.DoesNotContain("nav-link", inicio, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Depois_do_login_volta_para_a_pagina_local_pedida()
    {
        using var navegador = cenario.Painel.NovoNavegador();

        var resposta = await navegador.EntrarAsync(ApiHost.EmailAdmin, ApiHost.SenhaAdmin, returnUrl: "/health");

        Assert.Equal("/health", Navegador.Destino(resposta));
    }

    [Theory]
    [InlineData("https://site-malicioso.example/")]
    [InlineData("//site-malicioso.example/")]
    public async Task Endereco_externo_de_retorno_e_ignorado(string retorno)
    {
        using var navegador = cenario.Painel.NovoNavegador();

        var resposta = await navegador.EntrarAsync(ApiHost.EmailAdmin, ApiHost.SenhaAdmin, returnUrl: retorno);

        Assert.Equal("/", Navegador.Destino(resposta));
    }

    [Fact]
    public async Task Logout_encerra_a_sessao_e_exige_o_token_anti_csrf()
    {
        using var navegador = await cenario.EntrarComoAsync("Vendedor");
        var inicio = await navegador.PaginaAsync("/");

        var semToken = await navegador.EnviarSemTokenAsync("/Conta/Logout", []);
        var comToken = await navegador.EnviarComTokenDaPaginaAsync(inicio, "/Conta/Logout", []);
        var depois = await navegador.GetAsync("/");

        Assert.Equal(HttpStatusCode.BadRequest, semToken.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, comToken.StatusCode);
        Assert.Equal("/Conta/Login", Navegador.Destino(comToken));
        Assert.Equal(HttpStatusCode.Redirect, depois.StatusCode);
        Assert.StartsWith("/Conta/Login", Navegador.Destino(depois), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Usuario_ja_logado_que_abre_o_login_vai_para_o_inicio()
    {
        using var navegador = await cenario.EntrarComoAsync("Administrador");

        var resposta = await navegador.GetAsync("/Conta/Login");

        Assert.Equal(HttpStatusCode.Redirect, resposta.StatusCode);
        Assert.Equal("/", Navegador.Destino(resposta));
    }

    [Fact]
    public async Task Respostas_trazem_os_cabecalhos_de_seguranca()
    {
        using var navegador = cenario.Painel.NovoNavegador();

        using var resposta = await navegador.GetAsync("/Conta/Login");

        var csp = string.Join(";", resposta.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'self'", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
        Assert.Equal("nosniff", resposta.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", resposta.Headers.GetValues("X-Frame-Options").Single());
        Assert.False(resposta.Headers.Contains("Server"));
    }

    [Fact]
    public async Task Paginas_nao_usam_scripts_nem_estilos_embutidos_por_causa_da_politica_de_conteudo()
    {
        using var navegador = await cenario.EntrarComoAsync("Administrador");

        var inicio = await navegador.PaginaAsync("/");

        Assert.DoesNotContain(" style=\"", inicio, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"<script(?![^>]*\bsrc=)", inicio);
    }

    [Fact]
    public async Task Health_responde_sem_login()
    {
        using var navegador = cenario.Painel.NovoNavegador();

        using var resposta = await navegador.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
    }
}
