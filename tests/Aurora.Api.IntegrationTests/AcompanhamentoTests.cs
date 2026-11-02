using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Comum;
using Aurora.Application.Expedicoes;
using Aurora.Application.Pedidos;
using Aurora.Application.Qualidade;

namespace Aurora.Api.IntegrationTests;

/// <summary>Acompanhamento do pedido pela linha do tempo e consulta do cliente aos próprios pedidos (RF08, RF22).</summary>
[Collection("api")]
public class AcompanhamentoTests(ApiFactory api)
{
    private static Task<AcompanhamentoDto?> AcompanharAsync(HttpClient cliente, int pedidoId) =>
        cliente.GetFromJsonAsync<AcompanhamentoDto>($"/api/v1/pedidos/{pedidoId}/acompanhamento");

    [Fact]
    public async Task Linha_do_tempo_conta_a_historia_do_pedido_do_registro_a_expedicao()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        using var inspetor = await api.ComoAsync("Inspetor");
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 2);
        var antesDaProducao = await AcompanharAsync(vendedor, pedido.Id);
        var ordem = await CenarioDeProducao.AvancarAteAQualidadeAsync(gerente, operador, pedido.Id);
        (await inspetor.PostAsJsonAsync(
                $"/api/v1/ordens-producao/{ordem.Ordem.Id}/inspecoes", new InspecaoRequest("Aprovada", null, null)))
            .AssertStatus(HttpStatusCode.Created);
        (await almoxarife.PostAsync($"/api/v1/pedidos/{pedido.Id}/expedir", content: null)).AssertStatus(HttpStatusCode.Created);

        var completo = await AcompanharAsync(vendedor, pedido.Id);

        Assert.Equal(["Pedido registrado"], antesDaProducao!.Eventos.Select(e => e.Evento));
        Assert.Equal(pedido.Id, completo!.Pedido.Id);
        Assert.Equal("Expedido", completo.Pedido.Status);
        Assert.Equal(cenario.Produto.Codigo, Assert.Single(completo.Pedido.Itens).ProdutoCodigo);
        var eventos = completo.Eventos.Select(e => e.Evento).ToList();
        Assert.Equal("Pedido registrado", eventos[0]);
        Assert.Equal("Pedido expedido", eventos[^1]);
        Assert.Contains("Etapa iniciada: Corte", eventos);
        Assert.Contains("Etapa concluída: Corte", eventos);
        Assert.Contains("Etapa iniciada: Qualidade", eventos);
        Assert.Contains("Etapa concluída: Qualidade", eventos);
        Assert.Contains("Ordem de produção concluída", eventos);
        Assert.Equal(completo.Eventos.OrderBy(e => e.DataHora).Select(e => e.DataHora), completo.Eventos.Select(e => e.DataHora));
        Assert.Equal(ordem.Ordem.Numero, completo.Eventos.First(e => e.Evento == "Etapa iniciada: Corte").NumeroOp);
        Assert.Null(completo.Eventos[0].NumeroOp);
    }

    [Fact]
    public async Task Vendedor_so_acompanha_os_proprios_pedidos_e_os_demais_perfis_com_acesso_acompanham_todos()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var dono = await cenario.NovoVendedorAsync();
        using var outro = await cenario.NovoVendedorAsync();
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var almoxarife = await api.ComoAsync("Almoxarife");
        using var admin = await api.ComoAdministradorAsync();
        var pedido = await cenario.PedidoConfirmadoAsync(dono, quantidade: 1);
        var rota = $"/api/v1/pedidos/{pedido.Id}/acompanhamento";

        var doOutro = await outro.GetAsync(rota);

        doOutro.AssertStatus(HttpStatusCode.NotFound);
        Assert.Equal("PEDIDO_NAO_ENCONTRADO", await doOutro.CodigoAsync());
        foreach (var cliente in new[] { dono, gerente, almoxarife, admin })
        {
            (await cliente.GetAsync(rota)).AssertStatus(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task Acompanhar_pedido_inexistente_devolve_404()
    {
        using var gerente = await api.ComoAsync("GerenteProducao");

        var resposta = await gerente.GetAsync("/api/v1/pedidos/2147000000/acompanhamento");

        resposta.AssertStatus(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cliente_ve_so_os_proprios_pedidos_em_meus_pedidos_do_mais_recente_ao_mais_antigo()
    {
        var meu = await CenarioDeProducao.CriarAsync(api);
        var alheio = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var cliente = await meu.NovoClienteLogadoAsync();
        var primeiro = await meu.PedidoAbertoAsync(vendedor, quantidade: 1);
        var segundo = await meu.PedidoConfirmadoAsync(vendedor, quantidade: 2);
        var deOutroCliente = await alheio.PedidoAbertoAsync(vendedor, quantidade: 1);

        var lista = await cliente.GetFromJsonAsync<Pagina<PedidoResumoDto>>("/api/v1/meus-pedidos");

        Assert.Equal([segundo.Id, primeiro.Id], lista!.Items.Select(p => p.Id));
        Assert.Equal(2, lista.Total);
        Assert.All(lista.Items, p => Assert.Equal(meu.Cliente.Id, p.ClienteId));
        Assert.DoesNotContain(lista.Items, p => p.Id == deOutroCliente.Id);
    }

    [Fact]
    public async Task Meus_pedidos_filtra_por_status_e_pagina()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var cliente = await cenario.NovoClienteLogadoAsync();
        await cenario.PedidoAbertoAsync(vendedor, quantidade: 1);
        await cenario.PedidoAbertoAsync(vendedor, quantidade: 1);
        var emProducao = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);

        var abertos = await cliente.GetFromJsonAsync<Pagina<PedidoResumoDto>>("/api/v1/meus-pedidos?status=Aberto");
        var producao = await cliente.GetFromJsonAsync<Pagina<PedidoResumoDto>>("/api/v1/meus-pedidos?status=EmProducao");
        var paginaUm = await cliente.GetFromJsonAsync<Pagina<PedidoResumoDto>>("/api/v1/meus-pedidos?page=1&pageSize=2");
        var paginaDois = await cliente.GetFromJsonAsync<Pagina<PedidoResumoDto>>("/api/v1/meus-pedidos?page=2&pageSize=2");

        Assert.Equal(2, abertos!.Total);
        Assert.Equal(emProducao.Id, Assert.Single(producao!.Items).Id);
        Assert.Equal(3, paginaUm!.Total);
        Assert.Equal(2, paginaUm.Items.Count);
        Assert.Single(paginaDois!.Items);
    }

    [Fact]
    public async Task Cliente_acompanha_o_proprio_pedido_e_o_de_outro_cliente_devolve_404()
    {
        var meu = await CenarioDeProducao.CriarAsync(api);
        var alheio = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var cliente = await meu.NovoClienteLogadoAsync();
        var proprio = await meu.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var deOutro = await alheio.PedidoConfirmadoAsync(vendedor, quantidade: 1);

        var meuAcompanhamento = await AcompanharAsync(cliente, proprio.Id);
        var alheioResposta = await cliente.GetAsync($"/api/v1/pedidos/{deOutro.Id}/acompanhamento");

        Assert.Equal(proprio.Numero, meuAcompanhamento!.Pedido.Numero);
        Assert.NotEmpty(meuAcompanhamento.Eventos);
        alheioResposta.AssertStatus(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Cliente_nao_usa_as_rotas_internas_de_pedidos()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var cliente = await cenario.NovoClienteLogadoAsync();
        var pedido = await cenario.PedidoAbertoAsync(vendedor, quantidade: 1);

        (await cliente.GetAsync("/api/v1/pedidos")).AssertStatus(HttpStatusCode.Forbidden);
        (await cliente.GetAsync($"/api/v1/pedidos/{pedido.Id}")).AssertStatus(HttpStatusCode.Forbidden);
        (await cliente.PostAsync($"/api/v1/pedidos/{pedido.Id}/cancelar", content: null)).AssertStatus(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Meus_pedidos_e_so_do_cliente_nem_o_vendedor_nem_o_administrador_usam()
    {
        using var vendedor = await api.ComoAsync("Vendedor");
        using var admin = await api.ComoAdministradorAsync();

        (await vendedor.GetAsync("/api/v1/meus-pedidos")).AssertStatus(HttpStatusCode.Forbidden);
        (await admin.GetAsync("/api/v1/meus-pedidos")).AssertStatus(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Cliente_ve_a_expedicao_do_proprio_pedido_na_linha_do_tempo()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        using var inspetor = await api.ComoAsync("Inspetor");
        using var almoxarife = await api.ComoAsync("Almoxarife");
        using var cliente = await cenario.NovoClienteLogadoAsync();
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var ordem = await CenarioDeProducao.AvancarAteAQualidadeAsync(gerente, operador, pedido.Id);
        (await inspetor.PostAsJsonAsync(
                $"/api/v1/ordens-producao/{ordem.Ordem.Id}/inspecoes", new InspecaoRequest("Aprovada", null, null)))
            .AssertStatus(HttpStatusCode.Created);
        (await almoxarife.PostAsJsonAsync($"/api/v1/pedidos/{pedido.Id}/expedir", new ExpedirRequest(null, null)))
            .AssertStatus(HttpStatusCode.Created);

        var acompanhamento = await AcompanharAsync(cliente, pedido.Id);
        var meus = await cliente.GetFromJsonAsync<Pagina<PedidoResumoDto>>("/api/v1/meus-pedidos");

        Assert.Equal("Pedido expedido", acompanhamento!.Eventos[^1].Evento);
        Assert.Equal("Expedido", Assert.Single(meus!.Items).Status);
    }
}
