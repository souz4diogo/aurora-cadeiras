using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Expedicoes;
using Aurora.Application.Producao;
using Aurora.Application.Qualidade;

namespace Aurora.Api.IntegrationTests;

/// <summary>Matriz de permissões (docs/06 §3) das rotas da fase 4a: QR, fila, sincronização, qualidade, expedição e acompanhamento.</summary>
[Collection("api")]
public class PermissoesFase4Tests(ApiFactory api)
{
    private static readonly string[] Todos =
        ["Administrador", "GerenteProducao", "Operador", "Vendedor", "Almoxarife", "Inspetor", "Cliente"];

    private static TheoryData<string, bool> Matriz(params string[] permitidos)
    {
        var dados = new TheoryData<string, bool>();
        foreach (var perfil in Todos)
        {
            dados.Add(perfil, permitidos.Contains(perfil));
        }

        return dados;
    }

    public static TheoryData<string, bool> LeituraDeQr => Matriz("Operador", "Inspetor", "GerenteProducao");

    public static TheoryData<string, bool> OperadorOuInspetor => Matriz("Operador", "Inspetor");

    public static TheoryData<string, bool> SoInspetor => Matriz("Inspetor");

    public static TheoryData<string, bool> ConsultaDeInspecoes => Matriz("Inspetor", "GerenteProducao", "Administrador");

    public static TheoryData<string, bool> SoAlmoxarife => Matriz("Almoxarife");

    public static TheoryData<string, bool> ConsultaDeExpedicoes => Matriz("Almoxarife", "GerenteProducao", "Administrador", "Vendedor");

    public static TheoryData<string, bool> SoCliente => Matriz("Cliente");

    public static TheoryData<string, bool> Acompanhamento => Matriz("Administrador", "GerenteProducao", "Vendedor", "Almoxarife", "Cliente");

    private async Task AssertAsync(string perfil, bool permitido, Func<HttpClient, Task<HttpResponseMessage>> chamada)
    {
        using var cliente = await api.ComoAsync(perfil);

        var resposta = await chamada(cliente);

        if (permitido)
        {
            // Passou pela autorização: qualquer coisa que não seja 401 ou 403 (o recurso pode não existir)
            Assert.NotEqual(HttpStatusCode.Forbidden, resposta.StatusCode);
            Assert.NotEqual(HttpStatusCode.Unauthorized, resposta.StatusCode);
            return;
        }

        resposta.AssertStatus(HttpStatusCode.Forbidden);
        Assert.Equal("SEM_PERMISSAO", await resposta.CodigoAsync());
    }

    [Theory]
    [MemberData(nameof(LeituraDeQr))]
    public Task Localizar_op_pelo_qr(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.GetAsync($"/api/v1/ordens-producao/qr/{Guid.NewGuid()}"));

    [Theory]
    [MemberData(nameof(OperadorOuInspetor))]
    public Task Fila_da_estacao(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.GetAsync("/api/v1/etapas/minhas?estacaoId=1"));

    [Theory]
    [MemberData(nameof(OperadorOuInspetor))]
    public Task Sincronizar_apontamentos(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.PostAsJsonAsync("/api/v1/apontamentos/sincronizar", new SincronizacaoRequest([])));

    [Theory]
    [MemberData(nameof(SoInspetor))]
    public Task Registrar_inspecao(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.PostAsJsonAsync(
            "/api/v1/ordens-producao/2147000000/inspecoes", new InspecaoRequest("Aprovada", null, null)));

    [Theory]
    [MemberData(nameof(ConsultaDeInspecoes))]
    public Task Listar_inspecoes(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.GetAsync("/api/v1/inspecoes"));

    [Theory]
    [MemberData(nameof(SoAlmoxarife))]
    public Task Expedir_pedido(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.PostAsJsonAsync("/api/v1/pedidos/2147000000/expedir", new ExpedirRequest(null, null)));

    [Theory]
    [MemberData(nameof(ConsultaDeExpedicoes))]
    public Task Listar_expedicoes(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.GetAsync("/api/v1/expedicoes"));

    [Theory]
    [MemberData(nameof(SoCliente))]
    public Task Meus_pedidos(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.GetAsync("/api/v1/meus-pedidos"));

    [Theory]
    [MemberData(nameof(Acompanhamento))]
    public Task Acompanhamento_do_pedido(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.GetAsync("/api/v1/pedidos/2147000000/acompanhamento"));

    [Theory]
    [InlineData("GET", "/api/v1/ordens-producao/qr/b6d1c0f2-1d1e-4a7e-9d3b-3f7e5c2a9a10")]
    [InlineData("GET", "/api/v1/etapas/minhas?estacaoId=1")]
    [InlineData("POST", "/api/v1/apontamentos/sincronizar")]
    [InlineData("POST", "/api/v1/ordens-producao/1/inspecoes")]
    [InlineData("GET", "/api/v1/inspecoes")]
    [InlineData("POST", "/api/v1/pedidos/1/expedir")]
    [InlineData("GET", "/api/v1/expedicoes")]
    [InlineData("GET", "/api/v1/meus-pedidos")]
    [InlineData("GET", "/api/v1/pedidos/1/acompanhamento")]
    public async Task Sem_login_devolve_401(string metodo, string rota)
    {
        using var anonimo = api.CreateClient();

        var resposta = await anonimo.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), rota));

        resposta.AssertStatus(HttpStatusCode.Unauthorized);
    }
}
