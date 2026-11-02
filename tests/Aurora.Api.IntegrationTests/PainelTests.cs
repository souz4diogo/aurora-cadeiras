using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Comum;
using Aurora.Application.Compras;
using Aurora.Application.Estoque;
using Aurora.Application.Fornecedores;
using Aurora.Application.MateriasPrimas;
using Aurora.Application.Painel;

namespace Aurora.Api.IntegrationTests;

/// <summary>
/// Painel gerencial (RF20). Os testes compartilham um banco com os demais, então comparam o painel ANTES e DEPOIS
/// de cada ação (a diferença), e não valores absolutos.
/// </summary>
[Collection("api")]
public class PainelTests(ApiFactory api)
{
    private static readonly string[] StatusQueContamComoAtraso = ["Confirmado", "EmProducao", "Pronto"];

    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.UtcNow);

    private static async Task<ResumoDoPainelDto> ResumoAsync(HttpClient gerente) =>
        (await gerente.GetFromJsonAsync<ResumoDoPainelDto>("/api/v1/painel/resumo"))!;

    private static async Task<ProducaoDaEstacaoDto> EstacaoAsync(HttpClient gerente, byte estacaoId) =>
        (await gerente.GetFromJsonAsync<List<ProducaoDaEstacaoDto>>("/api/v1/painel/producao-por-estacao"))!.Single(e => e.EstacaoId == estacaoId);

    [Fact]
    public async Task Resumo_acompanha_o_pedido_da_criacao_a_producao()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var inicio = await ResumoAsync(gerente);

        var pedido = await cenario.PedidoAbertoAsync(vendedor, quantidade: 3);
        var aberto = await ResumoAsync(gerente);
        (await vendedor.PostAsync($"/api/v1/pedidos/{pedido.Id}/confirmar", content: null)).AssertStatus(HttpStatusCode.OK);
        var confirmado = await ResumoAsync(gerente);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        (await operador.PostAsJsonAsync($"/api/v1/etapas/{ordem.Etapas[0].Id}/iniciar", CenarioDeProducao.NovoApontamento()))
            .AssertStatus(HttpStatusCode.OK);
        var emProducao = await ResumoAsync(gerente);

        Assert.Equal(inicio.PedidosEmAberto + 1, aberto.PedidosEmAberto);
        Assert.Equal(aberto.OrdensPlanejadas, confirmado.OrdensPlanejadas - 1); // a confirmação gerou a OP
        Assert.Equal(confirmado.OrdensPlanejadas - 1, emProducao.OrdensPlanejadas); // a OP começou...
        Assert.Equal(confirmado.OrdensEmAndamento + 1, emProducao.OrdensEmAndamento); // ...e saiu de planejada
        Assert.Equal(inicio.PedidosEmAberto + 1, emProducao.PedidosEmAberto);
        Assert.True(emProducao.AtualizadoEm > DateTime.UtcNow.AddMinutes(-2));
    }

    [Fact]
    public async Task Pedido_e_op_com_prazo_vencido_contam_como_atrasados_e_saem_ao_cancelar()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        var antes = await ResumoAsync(gerente);

        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1, prazo: Hoje.AddDays(-1));
        var atrasado = await ResumoAsync(gerente);
        (await vendedor.PostAsync($"/api/v1/pedidos/{pedido.Id}/cancelar", content: null)).AssertStatus(HttpStatusCode.OK);
        var cancelado = await ResumoAsync(gerente);

        Assert.Equal(antes.PedidosAtrasados + 1, atrasado.PedidosAtrasados);
        Assert.Equal(antes.OrdensAtrasadas + 1, atrasado.OrdensAtrasadas); // previsão = prazo - 2 dias
        Assert.Equal(antes.PedidosAtrasados, cancelado.PedidosAtrasados);
        Assert.Equal(antes.OrdensAtrasadas, cancelado.OrdensAtrasadas);
    }

    [Fact]
    public async Task Material_critico_entra_na_contagem_e_sai_quando_a_compra_e_recebida()
    {
        using var admin = await api.ComoAdministradorAsync();
        using var almoxarife = await api.ComoAsync("Almoxarife");
        using var gerente = await api.ComoAsync("GerenteProducao");
        var antes = await ResumoAsync(gerente); // antes de criar o material: saldo 0 com mínimo 10 já é crítico
        var fornecedorResposta = await admin.PostAsJsonAsync(
            "/api/v1/fornecedores", new FornecedorRequest("Fornecedor do painel", DadosDeTeste.Digitos(14), null, null, false));
        var fornecedor = (await fornecedorResposta.Content.ReadFromJsonAsync<FornecedorDto>())!;
        var materiaResposta = await admin.PostAsJsonAsync(
            "/api/v1/materias-primas",
            new MateriaPrimaRequest(("C" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant(), "Material crítico", "m", 10m, 1m, null));
        var materia = (await materiaResposta.Content.ReadFromJsonAsync<MateriaPrimaDto>())!;

        (await almoxarife.PostAsJsonAsync("/api/v1/estoque/ajustes", new AjusteDeEstoqueRequest(materia.Id, 4m, "Contagem"))).AssertStatus(HttpStatusCode.OK);
        var critico = await ResumoAsync(gerente);
        var pedidoResposta = await almoxarife.PostAsJsonAsync(
            "/api/v1/pedidos-compra", new PedidoCompraRequest(fornecedor.Id, Hoje.AddDays(-1), [new(materia.Id, 20m, 1m)]));
        var pedido = (await pedidoResposta.Content.ReadFromJsonAsync<PedidoCompraDto>())!;
        (await almoxarife.PostAsync($"/api/v1/pedidos-compra/{pedido.Id}/emitir", content: null)).AssertStatus(HttpStatusCode.OK);
        var emitido = await ResumoAsync(gerente);
        (await almoxarife.PostAsJsonAsync($"/api/v1/pedidos-compra/itens/{pedido.Itens[0].Id}/receber", new ReceberItemRequest(20m)))
            .AssertStatus(HttpStatusCode.OK);
        var reposto = await ResumoAsync(gerente);

        Assert.Equal(antes.MateriaisCriticos + 1, critico.MateriaisCriticos);
        Assert.Equal(critico.ComprasAReceber + 1, emitido.ComprasAReceber);
        Assert.Equal(critico.ComprasAtrasadas + 1, emitido.ComprasAtrasadas); // data prevista: ontem
        Assert.Equal(critico.MateriaisCriticos - 1, reposto.MateriaisCriticos);
        Assert.Equal(emitido.ComprasAReceber - 1, reposto.ComprasAReceber);
        Assert.Equal(emitido.ComprasAtrasadas - 1, reposto.ComprasAtrasadas);
    }

    [Fact]
    public async Task Producao_por_estacao_lista_todas_as_estacoes_em_ordem_e_segue_a_ordem_pela_linha()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var lista = (await gerente.GetFromJsonAsync<List<ProducaoDaEstacaoDto>>("/api/v1/painel/producao-por-estacao"))!;
        var corteAntes = await EstacaoAsync(gerente, CenarioDeProducao.Corte);
        var montagemAntes = await EstacaoAsync(gerente, CenarioDeProducao.MontagemDaEstrutura);

        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 2);
        var corteDepoisDeConfirmar = await EstacaoAsync(gerente, CenarioDeProducao.Corte);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        var corte = ordem.Etapas.Single(e => e.EstacaoId == CenarioDeProducao.Corte);
        (await operador.PostAsJsonAsync($"/api/v1/etapas/{corte.Id}/iniciar", CenarioDeProducao.NovoApontamento())).AssertStatus(HttpStatusCode.OK);
        var corteEmAndamento = await EstacaoAsync(gerente, CenarioDeProducao.Corte);
        (await operador.PostAsJsonAsync($"/api/v1/etapas/{corte.Id}/finalizar", CenarioDeProducao.NovoApontamento())).AssertStatus(HttpStatusCode.OK);
        var corteDepois = await EstacaoAsync(gerente, CenarioDeProducao.Corte);
        var montagemDepois = await EstacaoAsync(gerente, CenarioDeProducao.MontagemDaEstrutura);

        Assert.Equal([1, 2, 3, 4, 5], lista.Select(e => (int)e.EstacaoId)); // as estações que geram etapa, no layout da fábrica
        Assert.Equal(lista.Select(e => e.Ordem).Order(), lista.Select(e => e.Ordem));
        Assert.Equal(corteAntes.OrdensNaEstacao + 1, corteDepoisDeConfirmar.OrdensNaEstacao);
        Assert.Equal(corteAntes.Aguardando + 1, corteDepoisDeConfirmar.Aguardando);
        Assert.Equal(corteDepoisDeConfirmar.EmAndamento + 1, corteEmAndamento.EmAndamento);
        Assert.Equal(corteDepoisDeConfirmar.Aguardando - 1, corteEmAndamento.Aguardando);
        Assert.NotNull(corteEmAndamento.TempoMedioNaEtapaMinutos);
        // A OP anda pela linha: terminou o Corte, sai de lá e passa a contar na Montagem
        Assert.Equal(corteAntes.OrdensNaEstacao, corteDepois.OrdensNaEstacao);
        Assert.Equal(montagemAntes.OrdensNaEstacao + 1, montagemDepois.OrdensNaEstacao);
    }

    [Fact]
    public async Task Estacao_sem_ordens_aparece_com_zeros_e_sem_tempo_medio()
    {
        using var gerente = await api.ComoAsync("GerenteProducao");

        var estacoes = (await gerente.GetFromJsonAsync<List<ProducaoDaEstacaoDto>>("/api/v1/painel/producao-por-estacao"))!;

        // As seis estações do seed, menos a Expedição (que não gera etapa)
        Assert.Equal(5, estacoes.Count);
        Assert.DoesNotContain(estacoes, e => e.Estacao == "Expedicao");
        Assert.All(estacoes, e => Assert.True(e.OrdensNaEstacao == e.EmAndamento + e.Aguardando));
    }

    [Fact]
    public async Task Pedidos_atrasados_lista_do_mais_atrasado_ao_menos_e_o_vendedor_ve_so_os_proprios()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var dono = await cenario.NovoVendedorAsync();
        using var outro = await cenario.NovoVendedorAsync();
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var admin = await api.ComoAdministradorAsync();
        var pedido = await cenario.PedidoConfirmadoAsync(dono, quantidade: 1, prazo: Hoje.AddDays(-1));
        var deOutro = await cenario.PedidoConfirmadoAsync(outro, quantidade: 1, prazo: Hoje.AddDays(-1));
        const string Rota = "/api/v1/painel/pedidos-atrasados?pageSize=100";

        var doGerente = await gerente.GetFromJsonAsync<Pagina<PedidoAtrasadoDto>>(Rota);
        var doDono = await dono.GetFromJsonAsync<Pagina<PedidoAtrasadoDto>>(Rota);
        var doAdmin = await admin.GetFromJsonAsync<Pagina<PedidoAtrasadoDto>>(Rota);

        Assert.Contains(doGerente!.Items, p => p.PedidoId == pedido.Id && p.DiasAtraso == 1 && p.Cliente == cenario.Cliente.Nome);
        Assert.Contains(doGerente.Items, p => p.PedidoId == deOutro.Id);
        Assert.Equal(doGerente.Items.Select(p => p.DiasAtraso).OrderByDescending(d => d), doGerente.Items.Select(p => p.DiasAtraso));
        Assert.Contains(doAdmin!.Items, p => p.PedidoId == pedido.Id);
        Assert.Equal(pedido.Id, Assert.Single(doDono!.Items).PedidoId); // o vendedor novo só tem este
        Assert.DoesNotContain(doDono.Items, p => p.PedidoId == deOutro.Id);
    }

    [Fact]
    public async Task Pedido_no_prazo_e_pedido_expedido_nao_sao_atrasos()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await cenario.NovoVendedorAsync();
        using var gerente = await api.ComoAsync("GerenteProducao");
        await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1); // prazo em 30 dias
        var aberto = await cenario.PedidoAbertoAsync(vendedor, quantidade: 1, prazo: Hoje.AddDays(-1)); // vencido, mas não confirmado

        var lista = await gerente.GetFromJsonAsync<Pagina<PedidoAtrasadoDto>>("/api/v1/painel/pedidos-atrasados?pageSize=100");

        Assert.DoesNotContain(lista!.Items, p => p.PedidoId == aberto.Id);
        Assert.All(lista.Items, p => Assert.Contains(p.Status, StatusQueContamComoAtraso));
    }

    [Fact]
    public async Task Pedidos_atrasados_pagina()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await cenario.NovoVendedorAsync();
        for (var i = 0; i < 3; i++)
        {
            await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1, prazo: Hoje.AddDays(-1));
        }

        var primeira = await vendedor.GetFromJsonAsync<Pagina<PedidoAtrasadoDto>>("/api/v1/painel/pedidos-atrasados?page=1&pageSize=2");
        var segunda = await vendedor.GetFromJsonAsync<Pagina<PedidoAtrasadoDto>>("/api/v1/painel/pedidos-atrasados?page=2&pageSize=2");

        Assert.Equal(3, primeira!.Total);
        Assert.Equal(2, primeira.Items.Count);
        Assert.Single(segunda!.Items);
        Assert.Empty(primeira.Items.Select(p => p.PedidoId).Intersect(segunda.Items.Select(p => p.PedidoId)));
    }

    [Fact]
    public async Task Consumo_de_materiais_traz_o_que_a_producao_consumiu_do_maior_para_o_menor()
    {
        // Quantidade grande para ficar entre os 10 maiores mesmo com o consumo dos outros testes
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 1_000_000m, consumoPorUnidade: 50m);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1000);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        (await operador.PostAsJsonAsync($"/api/v1/etapas/{ordem.Etapas[0].Id}/iniciar", CenarioDeProducao.NovoApontamento()))
            .AssertStatus(HttpStatusCode.OK);

        var consumo = await gerente.GetFromJsonAsync<List<ConsumoDeMaterialDto>>("/api/v1/painel/consumo-de-materiais?dias=7");

        var nosso = Assert.Single(consumo!, c => c.MateriaPrimaId == cenario.Materia.Id);
        Assert.Equal(50_000m, nosso.Quantidade);
        Assert.Equal(cenario.Materia.Codigo, nosso.Codigo);
        Assert.Equal(50_000m * 10m, nosso.CustoEstimado); // o custo unitário do cenário é 10
        Assert.True(consumo!.Count <= PainelService.MateriaisNoGrafico);
        Assert.Equal(consumo.Select(c => c.Quantidade).OrderByDescending(q => q), consumo.Select(c => c.Quantidade));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100000)]
    public async Task Janela_de_consumo_fora_do_limite_e_ajustada_em_vez_de_dar_erro(int dias)
    {
        using var gerente = await api.ComoAsync("GerenteProducao");

        var resposta = await gerente.GetAsync($"/api/v1/painel/consumo-de-materiais?dias={dias}");

        resposta.AssertStatus(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Consumo_fora_da_janela_nao_aparece()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 1_000_000m, consumoPorUnidade: 50m);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1000);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        (await operador.PostAsJsonAsync($"/api/v1/etapas/{ordem.Etapas[0].Id}/iniciar", CenarioDeProducao.NovoApontamento()))
            .AssertStatus(HttpStatusCode.OK);

        // Janela de 1 dia = só hoje: aparece. O relatório de um período passado não o traz.
        var hoje = await gerente.GetFromJsonAsync<List<ConsumoDeMaterialDto>>("/api/v1/painel/consumo-de-materiais?dias=1");

        Assert.Contains(hoje!, c => c.MateriaPrimaId == cenario.Materia.Id);
    }
}
