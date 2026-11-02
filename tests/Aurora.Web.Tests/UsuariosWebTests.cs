using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Aurora.Web.Tests.Infraestrutura;

namespace Aurora.Web.Tests;

[Collection("painel")]
public sealed partial class UsuariosWebTests(CenarioDoPainel cenario)
{
    private const byte Vendedor = 4;
    private const byte Almoxarife = 5;
    private const byte Cliente = 7;

    [GeneratedRegex(@"<code[^>]*>([^<]+)</code>")]
    private static partial Regex SenhaNaPagina();

    [GeneratedRegex(@"/Usuarios/Editar/(\d+)")]
    private static partial Regex LinkDeEdicao();

    private static string NovoEmail() => $"novo.{Guid.NewGuid():N}@teste.local";

    private static List<KeyValuePair<string, string>> Campos(string nome, string email, byte? perfil, string cliente = "") =>
    [
        new("Nome", nome),
        new("Email", email),
        new("PerfilId", perfil?.ToString(CultureInfo.InvariantCulture) ?? ""),
        new("ClienteId", cliente),
    ];

    private static string ExtrairSenha(string html) =>
        WebUtility.HtmlDecode(SenhaNaPagina().Match(html).Groups[1].Value);

    private static async Task<(string Email, string Senha, string Id)> CriarAsync(Navegador admin, string nome, byte perfil)
    {
        var email = NovoEmail();
        var envio = await admin.EnviarAsync("/Usuarios/Novo", "/Usuarios/Novo", Campos(nome, email, perfil));
        Assert.Equal(HttpStatusCode.OK, envio.StatusCode);
        var senha = ExtrairSenha(await envio.Content.ReadAsStringAsync());
        Assert.NotEqual("", senha);
        var lista = await admin.PaginaAsync($"/Usuarios?busca={Uri.EscapeDataString(email)}");
        return (email, senha, LinkDeEdicao().Match(lista).Groups[1].Value);
    }

    private async Task<HttpStatusCode> EntrarComoNovoAsync(string email, string senha)
    {
        using var navegador = cenario.Painel.NovoNavegador();
        return (await navegador.EntrarAsync(email, senha)).StatusCode;
    }

    [Fact]
    public async Task Administrador_cria_usuario_ve_a_senha_uma_vez_e_o_usuario_consegue_entrar()
    {
        using var admin = await cenario.EntrarComoAsync("Administrador");
        var email = NovoEmail();

        var envio = await admin.EnviarAsync("/Usuarios/Novo", "/Usuarios/Novo", Campos("Maria Vendedora", email, Vendedor));
        var pagina = Navegador.Decodificar(await envio.Content.ReadAsStringAsync());
        var senha = ExtrairSenha(pagina);
        var lista = Navegador.Decodificar(await admin.PaginaAsync($"/Usuarios?busca={Uri.EscapeDataString(email)}"));

        Assert.Equal(HttpStatusCode.OK, envio.StatusCode);
        Assert.Contains("Usuário criado", pagina, StringComparison.Ordinal);
        Assert.Contains("Ela não será mostrada de novo", pagina, StringComparison.Ordinal);
        Assert.True(senha.Length >= 10, "a senha provisória segue a política de 10 caracteres");
        Assert.Contains("no-store", envio.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        Assert.Contains("Maria Vendedora", lista, StringComparison.Ordinal);
        Assert.DoesNotContain(senha, lista, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, await EntrarComoNovoAsync(email, senha));
    }

    [Fact]
    public async Task Email_repetido_perfil_cliente_sem_cliente_e_campos_vazios_voltam_ao_formulario()
    {
        using var admin = await cenario.EntrarComoAsync("Administrador");
        var (email, _, _) = await CriarAsync(admin, "Original", Almoxarife);

        var repetido = await admin.EnviarAsync("/Usuarios/Novo", "/Usuarios/Novo", Campos("Outro", email, Vendedor));
        var semCliente = await admin.EnviarAsync("/Usuarios/Novo", "/Usuarios/Novo", Campos("Cliente sem vínculo", NovoEmail(), Cliente));
        var vazio = await admin.EnviarAsync("/Usuarios/Novo", "/Usuarios/Novo", []);

        Assert.Equal(HttpStatusCode.OK, repetido.StatusCode);
        Assert.Contains("Já existe um usuário com este e-mail.", Navegador.Decodificar(await repetido.Content.ReadAsStringAsync()), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, semCliente.StatusCode);
        Assert.Contains("O perfil Cliente exige", Navegador.Decodificar(await semCliente.Content.ReadAsStringAsync()), StringComparison.Ordinal);
        var textoVazio = Navegador.Decodificar(await vazio.Content.ReadAsStringAsync());
        Assert.Contains("Informe o nome.", textoVazio, StringComparison.Ordinal);
        Assert.Contains("Informe o e-mail.", textoVazio, StringComparison.Ordinal);
        Assert.Contains("Escolha o perfil.", textoVazio, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Editar_muda_nome_e_perfil_e_a_lista_mostra()
    {
        using var admin = await cenario.EntrarComoAsync("Administrador");
        var (email, _, id) = await CriarAsync(admin, "Antes da edição", Vendedor);

        var formulario = Navegador.Decodificar(await admin.PaginaAsync($"/Usuarios/Editar/{id}"));
        var envio = await admin.EnviarAsync($"/Usuarios/Editar/{id}", $"/Usuarios/Editar/{id}", Campos("Depois da edição", email, Almoxarife));
        var lista = Navegador.Decodificar(await admin.PaginaAsync($"/Usuarios?busca={Uri.EscapeDataString(email)}"));

        Assert.Contains("Antes da edição", formulario, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, envio.StatusCode);
        Assert.Contains("Depois da edição", lista, StringComparison.Ordinal);
        Assert.Contains("<td>Almoxarife</td>", lista, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Inativar_impede_o_login_e_ativar_devolve_o_acesso()
    {
        using var admin = await cenario.EntrarComoAsync("Administrador");
        var (email, senha, id) = await CriarAsync(admin, "Vai ser inativada", Vendedor);

        var inativar = await admin.EnviarAsync("/Usuarios", $"/Usuarios/AlterarAtivo/{id}", [new("ativo", "false")]);
        var aviso = Navegador.Decodificar(await admin.PaginaAsync(Navegador.Destino(inativar)));
        var inativoTenta = await EntrarComoNovoAsync(email, senha);
        await admin.EnviarAsync("/Usuarios", $"/Usuarios/AlterarAtivo/{id}", [new("ativo", "true")]);
        var ativoTenta = await EntrarComoNovoAsync(email, senha);

        Assert.Contains("inativado; as sessões dele foram encerradas", aviso, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, inativoTenta);
        Assert.Equal(HttpStatusCode.Redirect, ativoTenta);
    }

    [Fact]
    public async Task Redefinir_senha_pede_confirmacao_mostra_a_nova_senha_e_a_antiga_deixa_de_valer()
    {
        using var admin = await cenario.EntrarComoAsync("Administrador");
        var (email, antiga, id) = await CriarAsync(admin, "Esqueceu a senha", Vendedor);

        var confirmacao = Navegador.Decodificar(await admin.PaginaAsync($"/Usuarios/RedefinirSenha/{id}"));
        var envio = await admin.EnviarAsync($"/Usuarios/RedefinirSenha/{id}", $"/Usuarios/RedefinirSenha/{id}", []);
        var pagina = Navegador.Decodificar(await envio.Content.ReadAsStringAsync());
        var nova = ExtrairSenha(pagina);

        Assert.Contains("Sim, redefinir a senha", confirmacao, StringComparison.Ordinal);
        Assert.Contains("Senha redefinida", pagina, StringComparison.Ordinal);
        Assert.Contains("no-store", envio.Headers.CacheControl?.ToString(), StringComparison.Ordinal);
        Assert.NotEqual(antiga, nova);
        Assert.Equal(HttpStatusCode.OK, await EntrarComoNovoAsync(email, antiga));
        Assert.Equal(HttpStatusCode.Redirect, await EntrarComoNovoAsync(email, nova));
    }

    [Fact]
    public async Task Busca_filtra_por_nome_e_por_email()
    {
        using var admin = await cenario.EntrarComoAsync("Administrador");
        var (email, _, _) = await CriarAsync(admin, "Zélia Procurada", Almoxarife);

        var porNome = Navegador.Decodificar(await admin.PaginaAsync("/Usuarios?busca=Z%C3%A9lia%20Procurada"));
        var porEmail = Navegador.Decodificar(await admin.PaginaAsync($"/Usuarios?busca={Uri.EscapeDataString(email)}"));
        var nenhum = Navegador.Decodificar(await admin.PaginaAsync("/Usuarios?busca=nao-existe-" + Guid.NewGuid().ToString("N")));

        Assert.Contains(email, porNome, StringComparison.Ordinal);
        Assert.Contains("Zélia Procurada", porEmail, StringComparison.Ordinal);
        Assert.Contains("Nenhum usuário encontrado.", nenhum, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("GerenteProducao")]
    [InlineData("Almoxarife")]
    [InlineData("Vendedor")]
    [InlineData("Operador")]
    public async Task Demais_perfis_vao_para_acesso_negado_e_nao_veem_o_menu(string perfil)
    {
        using var navegador = await cenario.EntrarComoAsync(perfil);

        var lista = await navegador.GetAsync("/Usuarios");
        var novo = await navegador.GetAsync("/Usuarios/Novo");
        var envio = await navegador.EnviarSemTokenAsync("/Usuarios/AlterarAtivo/1", [new("ativo", "false")]);
        var inicio = await navegador.PaginaAsync("/");

        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(lista), StringComparison.Ordinal);
        Assert.StartsWith("/Conta/AcessoNegado", Navegador.Destino(novo), StringComparison.Ordinal);
        Assert.NotEqual(HttpStatusCode.OK, envio.StatusCode);
        Assert.DoesNotContain("href=\"/Usuarios\"", inicio, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Menu_do_administrador_oferece_usuarios_e_a_acao_exige_o_token_anti_csrf()
    {
        using var admin = await cenario.EntrarComoAsync("Administrador");

        var inicio = await admin.PaginaAsync("/");
        var semToken = await admin.EnviarSemTokenAsync("/Usuarios/AlterarAtivo/1", [new("ativo", "false")]);

        Assert.Contains("href=\"/Usuarios\"", inicio, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, semToken.StatusCode);
    }
}
