using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Producao;

namespace Aurora.Api.IntegrationTests;

/// <summary>Matriz de permissões (docs/06 §3) da produção, das etapas e dos saldos e alertas de estoque.</summary>
[Collection("api")]
public class PermissoesProducaoTests(ApiFactory api)
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

    public static TheoryData<string, bool> ConsultaDeOrdens => Matriz("Administrador", "GerenteProducao", "Vendedor", "Inspetor");

    public static TheoryData<string, bool> DetalheDeOrdem => Matriz("Administrador", "GerenteProducao", "Vendedor", "Inspetor", "Operador");

    public static TheoryData<string, bool> IniciarEtapa => Matriz("Operador", "Inspetor");

    public static TheoryData<string, bool> FinalizarEtapa => Matriz("Operador");

    public static TheoryData<string, bool> ConfirmarPedido => Matriz("Administrador", "Vendedor");

    public static TheoryData<string, bool> SaldoDeMateriaPrima => Matriz("Administrador", "GerenteProducao", "Almoxarife", "Operador");

    public static TheoryData<string, bool> ConsultaDeEstoque => Matriz("Administrador", "GerenteProducao", "Almoxarife");

    public static TheoryData<string, bool> LerAlerta => Matriz("Almoxarife");

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
    [MemberData(nameof(ConsultaDeOrdens))]
    public Task Listar_ops(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.GetAsync("/api/v1/ordens-producao"));

    [Theory]
    [MemberData(nameof(ConsultaDeOrdens))]
    public Task Quadro_de_producao(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.GetAsync("/api/v1/ordens-producao/quadro"));

    [Theory]
    [MemberData(nameof(DetalheDeOrdem))]
    public Task Detalhe_da_op(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.GetAsync("/api/v1/ordens-producao/2147000000"));

    [Theory]
    [MemberData(nameof(IniciarEtapa))]
    public Task Iniciar_etapa(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.PostAsJsonAsync("/api/v1/etapas/2147000000/iniciar", CenarioDeProducao.NovoApontamento()));

    [Theory]
    [MemberData(nameof(FinalizarEtapa))]
    public Task Finalizar_etapa(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.PostAsJsonAsync("/api/v1/etapas/2147000000/finalizar", CenarioDeProducao.NovoApontamento()));

    [Theory]
    [MemberData(nameof(FinalizarEtapa))]
    public Task Registrar_perda(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.PostAsJsonAsync("/api/v1/etapas/2147000000/perdas", new PerdaRequest("Refugo", 1, "Motivo")));

    [Theory]
    [MemberData(nameof(ConfirmarPedido))]
    public Task Confirmar_pedido(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.PostAsync("/api/v1/pedidos/2147000000/confirmar", content: null));

    [Theory]
    [MemberData(nameof(SaldoDeMateriaPrima))]
    public Task Saldo_de_materia_prima(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.GetAsync("/api/v1/estoque/materias-primas"));

    [Theory]
    [MemberData(nameof(ConsultaDeEstoque))]
    public Task Saldo_de_produto_acabado(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.GetAsync("/api/v1/estoque/produtos"));

    [Theory]
    [MemberData(nameof(ConsultaDeEstoque))]
    public Task Listar_alertas(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.GetAsync("/api/v1/estoque/alertas"));

    [Theory]
    [MemberData(nameof(LerAlerta))]
    public Task Marcar_alerta_como_lido(string perfil, bool permitido) =>
        AssertAsync(perfil, permitido, c => c.PostAsync("/api/v1/estoque/alertas/2147000000/ler", content: null));

    [Theory]
    [InlineData("GET", "/api/v1/ordens-producao")]
    [InlineData("GET", "/api/v1/ordens-producao/quadro")]
    [InlineData("GET", "/api/v1/ordens-producao/1")]
    [InlineData("POST", "/api/v1/etapas/1/iniciar")]
    [InlineData("POST", "/api/v1/etapas/1/finalizar")]
    [InlineData("POST", "/api/v1/etapas/1/perdas")]
    [InlineData("POST", "/api/v1/pedidos/1/confirmar")]
    [InlineData("GET", "/api/v1/estoque/materias-primas")]
    [InlineData("GET", "/api/v1/estoque/produtos")]
    [InlineData("GET", "/api/v1/estoque/alertas")]
    [InlineData("POST", "/api/v1/estoque/alertas/1/ler")]
    public async Task Sem_login_devolve_401(string metodo, string rota)
    {
        using var anonimo = api.CreateClient();

        var resposta = await anonimo.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), rota));

        resposta.AssertStatus(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Vendedor_so_enxerga_as_ops_dos_proprios_pedidos()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var dono = await cenario.NovoVendedorAsync();
        using var outro = await cenario.NovoVendedorAsync();
        using var gerente = await api.ComoAsync("GerenteProducao");
        var pedido = await cenario.PedidoConfirmadoAsync(dono, quantidade: 1);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);

        var proprias = await dono.GetFromJsonAsync<Aurora.Application.Comum.Pagina<OrdemProducaoResumoDto>>(
            $"/api/v1/ordens-producao?pedidoId={pedido.Id}");
        var alheias = await outro.GetFromJsonAsync<Aurora.Application.Comum.Pagina<OrdemProducaoResumoDto>>(
            $"/api/v1/ordens-producao?pedidoId={pedido.Id}");
        var detalheProprio = await dono.GetAsync($"/api/v1/ordens-producao/{ordem.Ordem.Id}");
        var detalheAlheio = await outro.GetAsync($"/api/v1/ordens-producao/{ordem.Ordem.Id}");
        var quadroAlheio = await outro.GetFromJsonAsync<QuadroDeProducaoDto>("/api/v1/ordens-producao/quadro");
        var confirmarAlheio = await outro.PostAsync($"/api/v1/pedidos/{pedido.Id}/confirmar", content: null);

        Assert.Single(proprias!.Items);
        Assert.Empty(alheias!.Items);
        detalheProprio.AssertStatus(HttpStatusCode.OK);
        detalheAlheio.AssertStatus(HttpStatusCode.NotFound);
        Assert.DoesNotContain(quadroAlheio!.Colunas.SelectMany(c => c.Ordens), o => o.Id == ordem.Ordem.Id);
        confirmarAlheio.AssertStatus(HttpStatusCode.NotFound);
    }
}
