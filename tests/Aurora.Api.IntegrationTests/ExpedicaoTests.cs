using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Comum;
using Aurora.Application.Estoque;
using Aurora.Application.Expedicoes;
using Aurora.Application.Pedidos;
using Aurora.Application.Qualidade;

namespace Aurora.Api.IntegrationTests;

/// <summary>Expedição (RF19, RN07): só pedido Pronto sai, e a saída baixa o produto acabado reservado.</summary>
[Collection("api")]
public class ExpedicaoTests(ApiFactory api)
{
    /// <summary>Leva um pedido de <paramref name="quantidade"/> unidades até Pronto, pela produção e pela inspeção.</summary>
    private async Task<PedidoDto> PedidoProntoAsync(CenarioDeProducao cenario, HttpClient vendedor, int quantidade)
    {
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        using var inspetor = await api.ComoAsync("Inspetor");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade);
        var ordem = await CenarioDeProducao.AvancarAteAQualidadeAsync(gerente, operador, pedido.Id);
        (await inspetor.PostAsJsonAsync(
                $"/api/v1/ordens-producao/{ordem.Ordem.Id}/inspecoes", new InspecaoRequest("Aprovada", null, null)))
            .AssertStatus(HttpStatusCode.Created);
        return pedido;
    }

    private static async Task<SaldoDeProdutoDto> SaldoDoProdutoAsync(HttpClient almoxarife, string codigo) =>
        (await almoxarife.GetFromJsonAsync<Pagina<SaldoDeProdutoDto>>($"/api/v1/estoque/produtos?busca={codigo}"))!
        .Items.Single(p => p.Codigo == codigo);

    [Fact]
    public async Task Expedir_pedido_pronto_baixa_o_produto_acabado_e_marca_o_pedido_como_expedido()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var pedido = await PedidoProntoAsync(cenario, vendedor, quantidade: 4);
        Assert.Equal(4, (await SaldoDoProdutoAsync(almoxarife, cenario.Produto.Codigo)).Reservado);

        var resposta = await almoxarife.PostAsJsonAsync(
            $"/api/v1/pedidos/{pedido.Id}/expedir", new ExpedirRequest("  Transportadora Sul  ", "BR123456789"));

        resposta.AssertStatus(HttpStatusCode.Created);
        var expedicao = (await resposta.Content.ReadFromJsonAsync<ExpedicaoDto>())!;
        Assert.Equal(pedido.Id, expedicao.PedidoId);
        Assert.Equal(pedido.Numero, expedicao.PedidoNumero);
        Assert.Equal(cenario.Cliente.Nome, expedicao.ClienteNome);
        Assert.Equal("Transportadora Sul", expedicao.Transportadora);
        Assert.Equal("BR123456789", expedicao.CodigoRastreio);
        Assert.False(string.IsNullOrEmpty(expedicao.UsuarioNome));
        var depois = await vendedor.GetFromJsonAsync<PedidoDto>($"/api/v1/pedidos/{pedido.Id}");
        Assert.Equal("Expedido", depois!.Status);
        var saldo = await SaldoDoProdutoAsync(almoxarife, cenario.Produto.Codigo);
        Assert.Equal(0, saldo.Quantidade);
        Assert.Equal(0, saldo.Reservado);
    }

    [Fact]
    public async Task Expedir_sem_corpo_funciona_porque_transportadora_e_rastreio_sao_opcionais()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var pedido = await PedidoProntoAsync(cenario, vendedor, quantidade: 1);

        var resposta = await almoxarife.PostAsync($"/api/v1/pedidos/{pedido.Id}/expedir", content: null);

        resposta.AssertStatus(HttpStatusCode.Created);
        var expedicao = (await resposta.Content.ReadFromJsonAsync<ExpedicaoDto>())!;
        Assert.Null(expedicao.Transportadora);
        Assert.Null(expedicao.CodigoRastreio);
    }

    [Fact]
    public async Task Expedir_duas_vezes_devolve_409_RN07_e_nao_baixa_o_estoque_de_novo()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var pedido = await PedidoProntoAsync(cenario, vendedor, quantidade: 2);
        (await almoxarife.PostAsync($"/api/v1/pedidos/{pedido.Id}/expedir", content: null)).AssertStatus(HttpStatusCode.Created);
        await cenario.DarEntradaDeProdutoAcabadoAsync(5);

        var repetida = await almoxarife.PostAsync($"/api/v1/pedidos/{pedido.Id}/expedir", content: null);

        repetida.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("RN07", await repetida.CodigoAsync());
        Assert.Equal(5, (await SaldoDoProdutoAsync(almoxarife, cenario.Produto.Codigo)).Quantidade);
    }

    [Fact]
    public async Task Expedir_pedido_ainda_em_producao_devolve_409_RN07()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);

        var resposta = await almoxarife.PostAsync($"/api/v1/pedidos/{pedido.Id}/expedir", content: null);

        resposta.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("RN07", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Expedir_pedido_aberto_devolve_409_RN07()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var pedido = await cenario.PedidoAbertoAsync(vendedor, quantidade: 1);

        var resposta = await almoxarife.PostAsync($"/api/v1/pedidos/{pedido.Id}/expedir", content: null);

        resposta.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("RN07", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Expedir_pedido_inexistente_devolve_404()
    {
        using var almoxarife = await api.ComoAsync("Almoxarife");

        var resposta = await almoxarife.PostAsync("/api/v1/pedidos/2147000000/expedir", content: null);

        resposta.AssertStatus(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(81, 0)]
    [InlineData(0, 41)]
    public async Task Transportadora_ou_rastreio_acima_do_limite_devolvem_400(int tamanhoTransportadora, int tamanhoRastreio)
    {
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var corpo = new ExpedirRequest(new string('t', tamanhoTransportadora), new string('r', tamanhoRastreio));

        var resposta = await almoxarife.PostAsJsonAsync("/api/v1/pedidos/2147000000/expedir", corpo);

        resposta.AssertStatus(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Listagem_traz_a_expedicao_e_o_vendedor_so_enxerga_as_dos_proprios_pedidos()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var dono = await cenario.NovoVendedorAsync();
        using var outro = await cenario.NovoVendedorAsync();
        using var almoxarife = await api.ComoAsync("Almoxarife");
        using var gerente = await api.ComoAsync("GerenteProducao");
        var pedido = await PedidoProntoAsync(cenario, dono, quantidade: 1);
        (await almoxarife.PostAsJsonAsync($"/api/v1/pedidos/{pedido.Id}/expedir", new ExpedirRequest("Rota", "AB1")))
            .AssertStatus(HttpStatusCode.Created);
        const string Rota = "/api/v1/expedicoes?pageSize=100";

        var doAlmoxarife = await almoxarife.GetFromJsonAsync<Pagina<ExpedicaoDto>>(Rota);
        var doGerente = await gerente.GetFromJsonAsync<Pagina<ExpedicaoDto>>(Rota);
        var doDono = await dono.GetFromJsonAsync<Pagina<ExpedicaoDto>>(Rota);
        var doOutro = await outro.GetFromJsonAsync<Pagina<ExpedicaoDto>>(Rota);

        Assert.Contains(doAlmoxarife!.Items, e => e.PedidoId == pedido.Id && e.CodigoRastreio == "AB1");
        Assert.Contains(doGerente!.Items, e => e.PedidoId == pedido.Id);
        Assert.Contains(doDono!.Items, e => e.PedidoId == pedido.Id);
        Assert.DoesNotContain(doOutro!.Items, e => e.PedidoId == pedido.Id);
        Assert.Empty(doOutro.Items);
    }

    [Fact]
    public async Task Listagem_filtra_por_periodo()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var pedido = await PedidoProntoAsync(cenario, vendedor, quantidade: 1);
        (await almoxarife.PostAsync($"/api/v1/pedidos/{pedido.Id}/expedir", content: null)).AssertStatus(HttpStatusCode.Created);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        var futuras = await almoxarife.GetFromJsonAsync<Pagina<ExpedicaoDto>>($"/api/v1/expedicoes?de={hoje.AddDays(2):yyyy-MM-dd}");
        var deHoje = await almoxarife.GetFromJsonAsync<Pagina<ExpedicaoDto>>(
            $"/api/v1/expedicoes?pageSize=100&de={hoje.AddDays(-1):yyyy-MM-dd}&ate={hoje.AddDays(1):yyyy-MM-dd}");

        Assert.Empty(futuras!.Items);
        Assert.Contains(deHoje!.Items, e => e.PedidoId == pedido.Id);
    }
}
