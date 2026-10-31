using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Comum;
using Aurora.Application.Estoque;
using Aurora.Application.Pedidos;
using Aurora.Application.Producao;

namespace Aurora.Api.IntegrationTests;

/// <summary>Saldos, reserva de produto acabado (RN02) e alertas de estoque mínimo (RN08) no fluxo da produção.</summary>
[Collection("api")]
public class EstoqueDeProducaoTests(ApiFactory api)
{
    private static async Task<List<AlertaDeEstoqueDto>> AlertasDoMaterialAsync(HttpClient cliente, int materiaPrimaId)
    {
        var encontrados = new List<AlertaDeEstoqueDto>();
        for (var pagina = 1; ; pagina++)
        {
            var lista = (await cliente.GetFromJsonAsync<Pagina<AlertaDeEstoqueDto>>($"/api/v1/estoque/alertas?page={pagina}&pageSize=100"))!;
            encontrados.AddRange(lista.Items.Where(a => a.MateriaPrimaId == materiaPrimaId));
            if (pagina * lista.PageSize >= lista.Total)
            {
                return encontrados;
            }
        }
    }

    private static async Task IniciarPrimeiraEtapaAsync(HttpClient operador, HttpClient gerente, int pedidoId)
    {
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedidoId);
        (await operador.PostAsJsonAsync($"/api/v1/etapas/{ordem.Etapas[0].Id}/iniciar", CenarioDeProducao.NovoApontamento()))
            .AssertStatus(HttpStatusCode.OK);
    }

    private static async Task<SaldoDeProdutoDto> SaldoDoProdutoAsync(HttpClient cliente, string codigo) =>
        (await cliente.GetFromJsonAsync<Pagina<SaldoDeProdutoDto>>($"/api/v1/estoque/produtos?busca={codigo}"))!.Items.Single(p => p.Codigo == codigo);

    [Fact]
    public async Task Iniciar_etapa_que_deixa_o_saldo_no_minimo_abre_um_alerta_com_os_dados_do_material()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 100m, estoqueMinimo: 95m, consumoPorUnidade: 2m);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 3);
        Assert.Empty(await AlertasDoMaterialAsync(gerente, cenario.Materia.Id));

        await IniciarPrimeiraEtapaAsync(operador, gerente, pedido.Id);

        var alerta = Assert.Single(await AlertasDoMaterialAsync(gerente, cenario.Materia.Id));
        Assert.Equal(cenario.Materia.Codigo, alerta.MateriaPrimaCodigo);
        Assert.Equal(94m, alerta.EstoqueAtual);
        Assert.Equal(95m, alerta.EstoqueMinimo);
        Assert.Equal("m2", alerta.Unidade);
        Assert.Null(alerta.LidoEm);
    }

    [Fact]
    public async Task Saldo_acima_do_minimo_nao_gera_alerta()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 100m, estoqueMinimo: 10m, consumoPorUnidade: 2m);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 3);

        await IniciarPrimeiraEtapaAsync(operador, gerente, pedido.Id);

        Assert.Empty(await AlertasDoMaterialAsync(gerente, cenario.Materia.Id));
    }

    [Fact]
    public async Task Ha_no_maximo_um_alerta_aberto_por_material_e_marcar_como_lido_permite_um_novo()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 100m, estoqueMinimo: 95m, consumoPorUnidade: 2m);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        using var almoxarife = await api.ComoAsync("Almoxarife");
        await IniciarPrimeiraEtapaAsync(operador, gerente, (await cenario.PedidoConfirmadoAsync(vendedor, 3)).Id);
        await IniciarPrimeiraEtapaAsync(operador, gerente, (await cenario.PedidoConfirmadoAsync(vendedor, 1)).Id);
        var primeiro = Assert.Single(await AlertasDoMaterialAsync(gerente, cenario.Materia.Id));

        (await almoxarife.PostAsync($"/api/v1/estoque/alertas/{primeiro.Id}/ler", content: null)).AssertStatus(HttpStatusCode.OK);
        Assert.Empty(await AlertasDoMaterialAsync(gerente, cenario.Materia.Id));
        await IniciarPrimeiraEtapaAsync(operador, gerente, (await cenario.PedidoConfirmadoAsync(vendedor, 1)).Id);

        var novo = Assert.Single(await AlertasDoMaterialAsync(gerente, cenario.Materia.Id));
        Assert.NotEqual(primeiro.Id, novo.Id);
        Assert.Equal(90m, novo.EstoqueAtual);
    }

    [Fact]
    public async Task Marcar_alerta_como_lido_grava_quem_leu_e_repetir_nao_muda_o_registro()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 10m, estoqueMinimo: 10m);
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var almoxarife = await api.ComoAsync("Almoxarife");
        // Ajustar para o mínimo dispara o alerta pelo trigger, sem precisar de produção
        (await almoxarife.PostAsJsonAsync("/api/v1/estoque/ajustes", new AjusteDeEstoqueRequest(cenario.Materia.Id, 4m, "Contagem")))
            .AssertStatus(HttpStatusCode.OK);
        var alerta = Assert.Single(await AlertasDoMaterialAsync(gerente, cenario.Materia.Id));

        var primeira = await almoxarife.PostAsync($"/api/v1/estoque/alertas/{alerta.Id}/ler", content: null);
        var lido = (await primeira.Content.ReadFromJsonAsync<AlertaDeEstoqueDto>())!;
        var repetida = await almoxarife.PostAsync($"/api/v1/estoque/alertas/{alerta.Id}/ler", content: null);
        var repetido = (await repetida.Content.ReadFromJsonAsync<AlertaDeEstoqueDto>())!;

        primeira.AssertStatus(HttpStatusCode.OK);
        Assert.NotNull(lido.LidoEm);
        Assert.NotNull(lido.LidoPorId);
        repetida.AssertStatus(HttpStatusCode.OK);
        Assert.Equal(lido.LidoEm, repetido.LidoEm);
        Assert.Equal(lido.LidoPorId, repetido.LidoPorId);
    }

    [Fact]
    public async Task Marcar_alerta_inexistente_como_lido_devolve_404()
    {
        using var almoxarife = await api.ComoAsync("Almoxarife");

        var resposta = await almoxarife.PostAsync("/api/v1/estoque/alertas/2147000000/ler", content: null);

        resposta.AssertStatus(HttpStatusCode.NotFound);
        Assert.Equal("ALERTA_NAO_ENCONTRADO", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Saldos_de_materia_prima_indicam_o_critico_e_filtram_por_busca_e_criticidade()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 5m, estoqueMinimo: 5m);
        using var operador = await api.ComoAsync("Operador");

        var criticos = (await operador.GetFromJsonAsync<Pagina<SaldoDeMateriaPrimaDto>>(
            $"/api/v1/estoque/materias-primas?critico=true&busca={cenario.Materia.Codigo}"))!;
        var normais = (await operador.GetFromJsonAsync<Pagina<SaldoDeMateriaPrimaDto>>(
            $"/api/v1/estoque/materias-primas?critico=false&busca={cenario.Materia.Codigo}"))!;

        var saldo = Assert.Single(criticos.Items);
        Assert.Equal(cenario.Materia.Id, saldo.Id);
        Assert.True(saldo.Critico);
        Assert.Equal(5m, saldo.EstoqueAtual);
        Assert.Empty(normais.Items);
    }

    [Fact]
    public async Task Saldos_de_materia_prima_nao_listam_as_inativas()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var admin = await api.ComoAdministradorAsync();
        (await admin.PatchAsJsonAsync($"/api/v1/materias-primas/{cenario.Materia.Id}/ativo", new AlterarAtivoRequest(false)))
            .AssertStatus(HttpStatusCode.OK);

        var saldos = (await admin.GetFromJsonAsync<Pagina<SaldoDeMateriaPrimaDto>>(
            $"/api/v1/estoque/materias-primas?busca={cenario.Materia.Codigo}"))!;

        Assert.Empty(saldos.Items);
    }

    [Fact]
    public async Task Pedido_atendido_pelo_estoque_de_produto_acabado_reserva_e_nao_gera_op_rn02()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        await cenario.DarEntradaDeProdutoAcabadoAsync(10);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");

        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 4);

        Assert.Equal("Pronto", pedido.Status);
        var ordens = await gerente.GetFromJsonAsync<Pagina<OrdemProducaoResumoDto>>($"/api/v1/ordens-producao?pedidoId={pedido.Id}");
        Assert.Empty(ordens!.Items);
        var saldo = await SaldoDoProdutoAsync(gerente, cenario.Produto.Codigo);
        Assert.Equal((10, 4, 6), (saldo.Quantidade, saldo.Reservado, saldo.Disponivel));
    }

    [Fact]
    public async Task Estoque_de_produto_que_cobre_so_parte_do_pedido_reserva_o_que_ha_e_gera_op_do_resto()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        await cenario.DarEntradaDeProdutoAcabadoAsync(3);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");

        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 5);

        Assert.Equal("EmProducao", pedido.Status);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        Assert.Equal(2, ordem.Ordem.Quantidade);
        var saldo = await SaldoDoProdutoAsync(gerente, cenario.Produto.Codigo);
        Assert.Equal((3, 3, 0), (saldo.Quantidade, saldo.Reservado, saldo.Disponivel));
    }

    [Fact]
    public async Task Cancelar_pedido_em_producao_libera_a_reserva_do_produto_acabado_e_cancela_a_op()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        await cenario.DarEntradaDeProdutoAcabadoAsync(10);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 14);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);

        (await vendedor.PostAsync($"/api/v1/pedidos/{pedido.Id}/cancelar", content: null)).AssertStatus(HttpStatusCode.OK);

        var saldo = await SaldoDoProdutoAsync(gerente, cenario.Produto.Codigo);
        Assert.Equal((10, 0, 10), (saldo.Quantidade, saldo.Reservado, saldo.Disponivel));
        var depois = await gerente.GetFromJsonAsync<OrdemProducaoDto>($"/api/v1/ordens-producao/{ordem.Ordem.Id}");
        Assert.Equal("Cancelada", depois!.Ordem.Status);
    }

    [Fact]
    public async Task Pedido_totalmente_reservado_fica_pronto_e_nao_pode_mais_ser_cancelado()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        await cenario.DarEntradaDeProdutoAcabadoAsync(10);
        using var vendedor = await api.ComoAsync("Vendedor");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 4);

        var cancelamento = await vendedor.PostAsync($"/api/v1/pedidos/{pedido.Id}/cancelar", content: null);

        cancelamento.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("STATUS_INVALIDO", await cancelamento.CodigoAsync());
    }

    [Fact]
    public async Task Produto_sem_linha_de_estoque_aparece_zerado_nos_saldos()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var almoxarife = await api.ComoAsync("Almoxarife");

        var saldo = await SaldoDoProdutoAsync(almoxarife, cenario.Produto.Codigo);

        Assert.Equal((0, 0, 0), (saldo.Quantidade, saldo.Reservado, saldo.Disponivel));
    }

    [Fact]
    public async Task Fluxo_completo_pedido_op_etapa_baixa_de_estoque_e_alerta()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 20m, estoqueMinimo: 10m, consumoPorUnidade: 2m);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        using var almoxarife = await api.ComoAsync("Almoxarife");

        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 5);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        var etapa = await operador.PostAsJsonAsync($"/api/v1/etapas/{ordem.Etapas[0].Id}/iniciar", CenarioDeProducao.NovoApontamento());

        etapa.AssertStatus(HttpStatusCode.OK);
        Assert.Equal("EmProducao", pedido.Status);
        Assert.Equal(10m, await cenario.SaldoAsync());
        var alerta = Assert.Single(await AlertasDoMaterialAsync(almoxarife, cenario.Materia.Id));
        Assert.Equal(10m, alerta.EstoqueAtual);

        var segundo = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var outraOrdem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, segundo.Id);
        (await operador.PostAsJsonAsync($"/api/v1/etapas/{outraOrdem.Etapas[0].Id}/iniciar", CenarioDeProducao.NovoApontamento()))
            .AssertStatus(HttpStatusCode.OK);

        Assert.Equal(8m, await cenario.SaldoAsync());
        Assert.Single(await AlertasDoMaterialAsync(almoxarife, cenario.Materia.Id));

        // Falta material: 8 de saldo para 5 unidades x 2 = 10
        var terceiro = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 5);
        var semMaterial = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, terceiro.Id);
        var rn04 = await operador.PostAsJsonAsync($"/api/v1/etapas/{semMaterial.Etapas[0].Id}/iniciar", CenarioDeProducao.NovoApontamento());
        rn04.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("RN04", await rn04.CodigoAsync());
        Assert.Equal(8m, await cenario.SaldoAsync());
    }
}
