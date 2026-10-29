using System.Net;
using System.Net.Http.Json;

namespace Aurora.Api.IntegrationTests;

/// <summary>Matriz de permissões (docs/06 §3) das rotas existentes: 200 / 401 / 403 por perfil.</summary>
[Collection("api")]
public class PermissoesTests(ApiFactory api)
{
    public static TheoryData<string> Perfis =>
        ["Administrador", "GerenteProducao", "Operador", "Vendedor", "Almoxarife", "Inspetor", "Cliente"];

    [Theory]
    [MemberData(nameof(Perfis))]
    public async Task Usuarios_e_perfis_so_para_administrador(string perfil)
    {
        using var cliente = await api.ComoAsync(perfil);
        var esperado = perfil == "Administrador" ? HttpStatusCode.OK : HttpStatusCode.Forbidden;

        var usuarios = await cliente.GetAsync("/api/v1/usuarios");
        var perfis = await cliente.GetAsync("/api/v1/perfis");

        usuarios.AssertStatus(esperado);
        perfis.AssertStatus(esperado);
        if (esperado == HttpStatusCode.Forbidden)
        {
            Assert.Equal("SEM_PERMISSAO", await usuarios.CodigoAsync());
        }
    }

    [Theory]
    [MemberData(nameof(Perfis))]
    public async Task Todos_os_perfis_acessam_me(string perfil)
    {
        using var cliente = await api.ComoAsync(perfil);

        var resposta = await cliente.GetAsync("/api/v1/auth/me");

        resposta.AssertStatus(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/v1/usuarios")]
    [InlineData("/api/v1/usuarios/1")]
    [InlineData("/api/v1/perfis")]
    [InlineData("/api/v1/auth/me")]
    public async Task Rotas_protegidas_sem_token_devolvem_401(string rota)
    {
        using var cliente = api.CreateClient();

        var resposta = await cliente.GetAsync(rota);

        resposta.AssertStatus(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Perfil_de_cliente_nao_acessa_nem_a_lista_de_usuarios_por_outro_metodo()
    {
        using var cliente = await api.ComoAsync("Cliente");

        var post = await cliente.PostAsync("/api/v1/usuarios", JsonContent.Create(new { }));
        var patch = await cliente.PatchAsync("/api/v1/usuarios/1/ativo", JsonContent.Create(new { ativo = false }));
        var reset = await cliente.PostAsync("/api/v1/usuarios/1/redefinir-senha", null);

        post.AssertStatus(HttpStatusCode.Forbidden);
        patch.AssertStatus(HttpStatusCode.Forbidden);
        reset.AssertStatus(HttpStatusCode.Forbidden);
    }
}
