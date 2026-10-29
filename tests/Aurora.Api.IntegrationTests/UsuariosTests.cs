using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Comum;
using Aurora.Application.Usuarios;
using Microsoft.Data.SqlClient;

namespace Aurora.Api.IntegrationTests;

[Collection("api")]
public class UsuariosTests(ApiFactory api)
{
    [Fact]
    public async Task Criar_usuario_devolve_201_com_senha_provisoria_e_o_login_funciona()
    {
        using var admin = await api.ComoAdministradorAsync();
        var email = $"novo.{Guid.NewGuid():N}@teste.local";

        var resposta = await admin.PostAsJsonAsync("/api/v1/usuarios", new CriarUsuarioRequest("Novo Usuário", email, 4, null));

        resposta.AssertStatus(HttpStatusCode.Created);
        var criado = (await resposta.Content.ReadFromJsonAsync<UsuarioComSenhaProvisoriaDto>())!;
        Assert.Equal($"/api/v1/usuarios/{criado.Usuario.Id}", resposta.Headers.Location?.ToString());
        Assert.True(criado.SenhaProvisoria.Length >= 10);
        Assert.Equal("Vendedor", criado.Usuario.Perfil);

        var login = await api.CreateClient().LoginAsync(email, criado.SenhaProvisoria);
        login.AssertStatus(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Senha_nunca_e_guardada_em_texto()
    {
        var (usuario, senha) = await api.CriarUsuarioAsync("Vendedor");

        await using var conexao = new SqlConnection(api.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT SenhaHash FROM dbo.Usuario WHERE UsuarioId = @id";
        comando.Parameters.AddWithValue("@id", usuario.Id);
        var hash = (string)(await comando.ExecuteScalarAsync())!;

        Assert.NotEqual(senha, hash);
        Assert.DoesNotContain(senha, hash, StringComparison.Ordinal);
        Assert.StartsWith("AQAAAA", hash, StringComparison.Ordinal); // formato v3 do PasswordHasher (PBKDF2)
    }

    [Fact]
    public async Task Email_duplicado_devolve_409()
    {
        var (existente, _) = await api.CriarUsuarioAsync("Vendedor");
        using var admin = await api.ComoAdministradorAsync();

        var resposta = await admin.PostAsJsonAsync(
            "/api/v1/usuarios", new CriarUsuarioRequest("Outro", existente.Email.ToUpperInvariant(), 4, null));

        resposta.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("EMAIL_DUPLICADO", await resposta.CodigoAsync());
    }

    [Theory]
    [InlineData("", "valido@teste.local", (byte)4, null)]
    [InlineData("Nome", "nao-e-email", (byte)4, null)]
    [InlineData("Nome", "valido@teste.local", (byte)99, null)]
    [InlineData("Nome", "valido@teste.local", (byte)7, null)]
    public async Task Entrada_invalida_devolve_400(string nome, string email, byte perfil, int? clienteId)
    {
        using var admin = await api.ComoAdministradorAsync();

        var resposta = await admin.PostAsJsonAsync("/api/v1/usuarios", new CriarUsuarioRequest(nome, email, perfil, clienteId));

        resposta.AssertStatus(HttpStatusCode.BadRequest);
        Assert.Equal("VALIDACAO", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Perfil_cliente_com_cliente_inexistente_devolve_422()
    {
        using var admin = await api.ComoAdministradorAsync();

        var resposta = await admin.PostAsJsonAsync(
            "/api/v1/usuarios", new CriarUsuarioRequest("Cliente X", $"c.{Guid.NewGuid():N}@teste.local", 7, 999999));

        resposta.AssertStatus(HttpStatusCode.UnprocessableEntity);
        Assert.Equal("CLIENTE_INEXISTENTE", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Usuario_inexistente_devolve_404()
    {
        using var admin = await api.ComoAdministradorAsync();

        var resposta = await admin.GetAsync("/api/v1/usuarios/987654");

        resposta.AssertStatus(HttpStatusCode.NotFound);
        Assert.Equal("USUARIO_NAO_ENCONTRADO", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Atualizar_altera_nome_e_perfil()
    {
        var (usuario, _) = await api.CriarUsuarioAsync("Vendedor");
        using var admin = await api.ComoAdministradorAsync();

        var resposta = await admin.PutAsJsonAsync(
            $"/api/v1/usuarios/{usuario.Id}", new AtualizarUsuarioRequest("Nome Novo", usuario.Email, 5, null));

        resposta.AssertStatus(HttpStatusCode.OK);
        var atualizado = (await resposta.Content.ReadFromJsonAsync<UsuarioDto>())!;
        Assert.Equal("Nome Novo", atualizado.Nome);
        Assert.Equal("Almoxarife", atualizado.Perfil);
    }

    [Fact]
    public async Task Inativar_impede_o_login_e_revoga_as_sessoes_e_reativar_restaura()
    {
        var (usuario, senha) = await api.CriarUsuarioAsync("Operador");
        var token = await api.LoginAsync(usuario.Email, senha);
        using var admin = await api.ComoAdministradorAsync();
        using var anonimo = api.CreateClient();

        (await admin.PatchAsJsonAsync($"/api/v1/usuarios/{usuario.Id}/ativo", new AlterarAtivoRequest(false)))
            .AssertStatus(HttpStatusCode.OK);

        (await anonimo.LoginAsync(usuario.Email, senha)).AssertStatus(HttpStatusCode.Unauthorized);
        (await anonimo.PostAsJsonAsync("/api/v1/auth/refresh", new Application.Autenticacao.RefreshRequest(token.RefreshToken)))
            .AssertStatus(HttpStatusCode.Unauthorized);

        (await admin.PatchAsJsonAsync($"/api/v1/usuarios/{usuario.Id}/ativo", new AlterarAtivoRequest(true)))
            .AssertStatus(HttpStatusCode.OK);
        (await anonimo.LoginAsync(usuario.Email, senha)).AssertStatus(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Redefinir_senha_invalida_a_antiga_e_revoga_as_sessoes()
    {
        var (usuario, senhaAntiga) = await api.CriarUsuarioAsync("Operador");
        var token = await api.LoginAsync(usuario.Email, senhaAntiga);
        using var admin = await api.ComoAdministradorAsync();
        using var anonimo = api.CreateClient();

        var resposta = await admin.PostAsync($"/api/v1/usuarios/{usuario.Id}/redefinir-senha", null);

        resposta.AssertStatus(HttpStatusCode.OK);
        var nova = (await resposta.Content.ReadFromJsonAsync<UsuarioComSenhaProvisoriaDto>())!.SenhaProvisoria;
        Assert.NotEqual(senhaAntiga, nova);
        (await anonimo.LoginAsync(usuario.Email, senhaAntiga)).AssertStatus(HttpStatusCode.Unauthorized);
        (await anonimo.LoginAsync(usuario.Email, nova)).AssertStatus(HttpStatusCode.OK);
        (await anonimo.PostAsJsonAsync("/api/v1/auth/refresh", new Application.Autenticacao.RefreshRequest(token.RefreshToken)))
            .AssertStatus(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Listagem_e_paginada_com_limite_de_100_e_aceita_busca()
    {
        var (usuario, _) = await api.CriarUsuarioAsync("Vendedor", $"busca.{Guid.NewGuid():N}@teste.local");
        using var admin = await api.ComoAdministradorAsync();

        var grande = await admin.GetFromJsonAsync<Pagina<UsuarioDto>>("/api/v1/usuarios?pageSize=500");
        var filtrada = await admin.GetFromJsonAsync<Pagina<UsuarioDto>>($"/api/v1/usuarios?busca={usuario.Email}");

        Assert.Equal(100, grande!.PageSize);
        Assert.Single(filtrada!.Items);
        Assert.Equal(usuario.Id, filtrada.Items[0].Id);
        Assert.Equal(1, filtrada.Total);
    }

    [Fact]
    public async Task Alteracoes_sao_auditadas_com_o_usuario_da_sessao()
    {
        using var admin = await api.ComoAdministradorAsync();
        var adminId = (await admin.GetFromJsonAsync<Application.Autenticacao.MeDto>("/api/v1/auth/me"))!.Usuario.Id;
        var (usuario, _) = await api.CriarUsuarioAsync("Vendedor");
        await admin.PatchAsJsonAsync($"/api/v1/usuarios/{usuario.Id}/ativo", new AlterarAtivoRequest(false));

        await using var conexao = new SqlConnection(api.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            "SELECT COUNT(*) FROM dbo.Auditoria WHERE Tabela = 'Usuario' AND Registro = CAST(@id AS varchar(50)) " +
            "AND Acao = 'U' AND UsuarioId = @admin AND Depois LIKE '%\"Ativo\":false%' AND Depois NOT LIKE '%SenhaHash%'";
        comando.Parameters.AddWithValue("@id", usuario.Id);
        comando.Parameters.AddWithValue("@admin", adminId);

        Assert.Equal(1, (int)(await comando.ExecuteScalarAsync())!);
    }
}
