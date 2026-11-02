using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Compras;

namespace Aurora.Api.IntegrationTests;

/// <summary>Matriz de permissões (docs/06 §3) das rotas da fase 5: compras, painel e relatórios.</summary>
[Collection("api")]
public class PermissoesFase5Tests(ApiFactory api)
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

    public static TheoryData<string, bool> ConsultaDeCompras => Matriz("Almoxarife", "GerenteProducao", "Administrador");

    public static TheoryData<string, bool> EscritaDeCompras => Matriz("Almoxarife");

    public static TheoryData<string, bool> Painel => Matriz("GerenteProducao", "Administrador");

    public static TheoryData<string, bool> PedidosAtrasados => Matriz("GerenteProducao", "Administrador", "Vendedor");

    public static TheoryData<string, bool> AreaDeRelatorios => Matriz("GerenteProducao", "Administrador", "Vendedor", "Almoxarife");

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
    [MemberData(nameof(ConsultaDeCompras))]
    public Task Listar_pedidos_de_compra(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.GetAsync("/api/v1/pedidos-compra"));

    [Theory]
    [MemberData(nameof(ConsultaDeCompras))]
    public Task Detalhe_do_pedido_de_compra(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.GetAsync("/api/v1/pedidos-compra/2147000000"));

    [Theory]
    [MemberData(nameof(EscritaDeCompras))]
    public Task Criar_pedido_de_compra(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.PostAsJsonAsync("/api/v1/pedidos-compra", new PedidoCompraRequest(2147000000, null, [new(1, 1m, 1m)])));

    [Theory]
    [MemberData(nameof(EscritaDeCompras))]
    public Task Emitir_pedido_de_compra(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.PostAsync("/api/v1/pedidos-compra/2147000000/emitir", content: null));

    [Theory]
    [MemberData(nameof(EscritaDeCompras))]
    public Task Cancelar_pedido_de_compra(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.PostAsync("/api/v1/pedidos-compra/2147000000/cancelar", content: null));

    [Theory]
    [MemberData(nameof(EscritaDeCompras))]
    public Task Receber_item_de_compra(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.PostAsJsonAsync("/api/v1/pedidos-compra/itens/2147000000/receber", new ReceberItemRequest(1m)));

    [Theory]
    [MemberData(nameof(Painel))]
    public Task Resumo_do_painel(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.GetAsync("/api/v1/painel/resumo"));

    [Theory]
    [MemberData(nameof(Painel))]
    public Task Producao_por_estacao(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.GetAsync("/api/v1/painel/producao-por-estacao"));

    [Theory]
    [MemberData(nameof(Painel))]
    public Task Consumo_de_materiais(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.GetAsync("/api/v1/painel/consumo-de-materiais"));

    [Theory]
    [MemberData(nameof(PedidosAtrasados))]
    public Task Pedidos_atrasados(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.GetAsync("/api/v1/painel/pedidos-atrasados"));

    [Theory]
    [MemberData(nameof(AreaDeRelatorios))]
    public Task Lista_de_relatorios(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.GetAsync("/api/v1/relatorios"));

    [Theory]
    [InlineData("GET", "/api/v1/pedidos-compra")]
    [InlineData("GET", "/api/v1/pedidos-compra/1")]
    [InlineData("POST", "/api/v1/pedidos-compra")]
    [InlineData("POST", "/api/v1/pedidos-compra/1/emitir")]
    [InlineData("POST", "/api/v1/pedidos-compra/1/cancelar")]
    [InlineData("POST", "/api/v1/pedidos-compra/itens/1/receber")]
    [InlineData("GET", "/api/v1/painel/resumo")]
    [InlineData("GET", "/api/v1/painel/producao-por-estacao")]
    [InlineData("GET", "/api/v1/painel/pedidos-atrasados")]
    [InlineData("GET", "/api/v1/painel/consumo-de-materiais")]
    [InlineData("GET", "/api/v1/relatorios")]
    [InlineData("GET", "/api/v1/relatorios/pedidos?formato=pdf")]
    [InlineData("GET", "/api/v1/relatorios/estoque?formato=xlsx")]
    public async Task Sem_login_devolve_401(string metodo, string rota)
    {
        using var anonimo = api.CreateClient();

        var resposta = await anonimo.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), rota));

        resposta.AssertStatus(HttpStatusCode.Unauthorized);
    }
}
