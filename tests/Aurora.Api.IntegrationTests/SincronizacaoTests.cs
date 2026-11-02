using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Comum;
using Aurora.Application.Producao;

namespace Aurora.Api.IntegrationTests;

/// <summary>Sincronização do lote de apontamentos offline (RF23): em ordem, idempotente e com resultado por item.</summary>
[Collection("api")]
public class SincronizacaoTests(ApiFactory api)
{
    private const string Rota = "/api/v1/apontamentos/sincronizar";

    private static ApontamentoOfflineItem Item(string tipo, int etapaId, Guid? requisicao = null, DateTimeOffset? hora = null) =>
        new(requisicao ?? Guid.NewGuid(), tipo, etapaId, hora ?? DateTimeOffset.UtcNow.AddMinutes(-5));

    private static async Task<IReadOnlyList<ResultadoDeSincronizacaoDto>> SincronizarAsync(
        HttpClient cliente, params ApontamentoOfflineItem[] itens)
    {
        var resposta = await cliente.PostAsJsonAsync(Rota, new SincronizacaoRequest(itens));
        resposta.AssertStatus(HttpStatusCode.OK);
        var corpo = (await resposta.Content.ReadFromJsonAsync<SincronizacaoResponse>())!;
        Assert.Equal(itens.Length, corpo.Resultados.Count);
        return corpo.Resultados;
    }

    private static async Task<EtapaDto> EtapaAsync(HttpClient gerente, int pedidoId, byte estacaoId) =>
        (await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedidoId)).Etapas.Single(e => e.EstacaoId == estacaoId);

    [Fact]
    public async Task Lote_offline_com_inicio_e_fim_aplica_os_dois_na_ordem_e_baixa_o_estoque_uma_vez()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 100m, consumoPorUnidade: 2m);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 5);
        var corte = await EtapaAsync(gerente, pedido.Id, CenarioDeProducao.Corte);
        var inicio = Item("Inicio", corte.Id, hora: DateTimeOffset.UtcNow.AddMinutes(-30));
        var fim = Item("Fim", corte.Id, hora: DateTimeOffset.UtcNow.AddMinutes(-10));

        var resultados = await SincronizarAsync(operador, inicio, fim);

        Assert.Equal([inicio.ClientRequestId, fim.ClientRequestId], resultados.Select(r => r.ClientRequestId));
        Assert.All(resultados, r => Assert.Equal("Aplicado", r.Status));
        Assert.Equal("Concluida", (await EtapaAsync(gerente, pedido.Id, CenarioDeProducao.Corte)).Status);
        Assert.Equal(90m, await cenario.SaldoAsync());
    }

    [Fact]
    public async Task Reenviar_o_mesmo_lote_devolve_JaProcessado_sem_baixar_o_estoque_de_novo()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 100m, consumoPorUnidade: 2m);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 5);
        var corte = await EtapaAsync(gerente, pedido.Id, CenarioDeProducao.Corte);
        var lote = new[] { Item("Inicio", corte.Id), Item("Fim", corte.Id) };
        await SincronizarAsync(operador, lote);

        var reenvio = await SincronizarAsync(operador, lote);

        Assert.All(reenvio, r => Assert.Equal("JaProcessado", r.Status));
        Assert.Equal(90m, await cenario.SaldoAsync());
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        Assert.Equal(2, ordem.Apontamentos.Count);
    }

    [Fact]
    public async Task Reenviar_so_o_item_ja_aplicado_dentro_de_um_lote_novo_aplica_apenas_o_novo()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var corte = await EtapaAsync(gerente, pedido.Id, CenarioDeProducao.Corte);
        var inicio = Item("Inicio", corte.Id);
        await SincronizarAsync(operador, inicio);

        var resultados = await SincronizarAsync(operador, inicio, Item("Fim", corte.Id));

        Assert.Equal(["JaProcessado", "Aplicado"], resultados.Select(r => r.Status));
    }

    [Fact]
    public async Task Rejeicao_parcial_nao_derruba_os_demais_itens_do_lote()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        var corte = ordem.Etapas.Single(e => e.EstacaoId == CenarioDeProducao.Corte);
        var montagem = ordem.Etapas.Single(e => e.EstacaoId == CenarioDeProducao.MontagemDaEstrutura);

        var resultados = await SincronizarAsync(
            operador,
            Item("Inicio", montagem.Id), // a anterior (Corte) ainda não terminou
            Item("Inicio", corte.Id), // vale
            Item("Inicio", 2147000000), // não existe
            Item("Pausa", corte.Id), // tipo desconhecido
            Item("Fim", corte.Id)); // vale, porque o início acima foi aplicado

        Assert.Equal(["Rejeitado", "Aplicado", "Rejeitado", "Rejeitado", "Aplicado"], resultados.Select(r => r.Status));
        Assert.Equal("RN05", resultados[0].Code);
        Assert.False(string.IsNullOrWhiteSpace(resultados[0].Mensagem));
        Assert.Equal("ETAPA_NAO_ENCONTRADA", resultados[2].Code);
        Assert.Equal("VALIDACAO", resultados[3].Code);
        Assert.Equal("Concluida", (await EtapaAsync(gerente, pedido.Id, CenarioDeProducao.Corte)).Status);
        Assert.Equal("Pendente", (await EtapaAsync(gerente, pedido.Id, CenarioDeProducao.MontagemDaEstrutura)).Status);
    }

    [Fact]
    public async Task Item_sem_saldo_de_materia_prima_e_rejeitado_com_RN04_e_o_proximo_item_segue()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 1m, consumoPorUnidade: 2m);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var comFalta = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 5);
        var corte = await EtapaAsync(gerente, comFalta.Id, CenarioDeProducao.Corte);

        var resultados = await SincronizarAsync(operador, Item("Inicio", corte.Id), Item("Inicio", 2147000000));

        Assert.Equal("Rejeitado", resultados[0].Status);
        Assert.Equal("RN04", resultados[0].Code);
        Assert.Equal("ETAPA_NAO_ENCONTRADA", resultados[1].Code);
        Assert.Equal(1m, await cenario.SaldoAsync());
        Assert.Equal("Pendente", (await EtapaAsync(gerente, comFalta.Id, CenarioDeProducao.Corte)).Status);
    }

    [Fact]
    public async Task Item_rejeitado_pode_ser_reenviado_depois_de_corrigida_a_causa()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 1m, consumoPorUnidade: 2m);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 5);
        var corte = await EtapaAsync(gerente, pedido.Id, CenarioDeProducao.Corte);
        var inicio = Item("Inicio", corte.Id);
        var primeira = await SincronizarAsync(operador, inicio);
        Assert.Equal("RN04", primeira[0].Code);
        (await almoxarife.PostAsJsonAsync(
                "/api/v1/estoque/ajustes",
                new Aurora.Application.Estoque.AjusteDeEstoqueRequest(cenario.Materia.Id, 100m, "Reposição")))
            .AssertStatus(HttpStatusCode.OK);

        var segunda = await SincronizarAsync(operador, inicio);

        Assert.Equal("Aplicado", segunda[0].Status);
        Assert.Equal(90m, await cenario.SaldoAsync());
    }

    [Fact]
    public async Task O_mesmo_identificador_repetido_no_proprio_lote_aplica_so_o_primeiro()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 100m, consumoPorUnidade: 2m);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 5);
        var corte = await EtapaAsync(gerente, pedido.Id, CenarioDeProducao.Corte);
        var inicio = Item("Inicio", corte.Id);

        var resultados = await SincronizarAsync(operador, inicio, inicio);

        Assert.Equal(["Aplicado", "JaProcessado"], resultados.Select(r => r.Status));
        Assert.Equal(90m, await cenario.SaldoAsync());
    }

    [Fact]
    public async Task Inspetor_pode_iniciar_a_qualidade_mas_nao_finalizar_etapa_pelo_lote()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        using var inspetor = await api.ComoAsync("Inspetor");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        foreach (var etapa in ordem.Etapas.Where(e => e.EstacaoId != CenarioDeProducao.Qualidade).OrderBy(e => e.Sequencia))
        {
            await SincronizarAsync(operador, Item("Inicio", etapa.Id), Item("Fim", etapa.Id));
        }

        var qualidade = ordem.Etapas.Single(e => e.EstacaoId == CenarioDeProducao.Qualidade);
        var resultados = await SincronizarAsync(inspetor, Item("Inicio", qualidade.Id), Item("Fim", qualidade.Id));

        Assert.Equal("Aplicado", resultados[0].Status);
        Assert.Equal("Rejeitado", resultados[1].Status);
        Assert.Equal("PERFIL_SEM_PERMISSAO", resultados[1].Code);
        Assert.Equal("EmAndamento", (await EtapaAsync(gerente, pedido.Id, CenarioDeProducao.Qualidade)).Status);
    }

    [Fact]
    public async Task Hora_do_aparelho_no_futuro_e_rejeitada_com_VALIDACAO()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var corte = await EtapaAsync(gerente, pedido.Id, CenarioDeProducao.Corte);

        var resultados = await SincronizarAsync(operador, Item("Inicio", corte.Id, hora: DateTimeOffset.UtcNow.AddHours(2)));

        Assert.Equal("Rejeitado", resultados[0].Status);
        Assert.Equal("VALIDACAO", resultados[0].Code);
    }

    [Fact]
    public async Task Identificador_vazio_e_rejeitado_com_VALIDACAO()
    {
        using var operador = await api.ComoAsync("Operador");

        var resultados = await SincronizarAsync(operador, Item("Inicio", 1, Guid.Empty));

        Assert.Equal("Rejeitado", resultados[0].Status);
        Assert.Equal("VALIDACAO", resultados[0].Code);
    }

    [Fact]
    public async Task Lote_vazio_ou_ausente_devolve_400()
    {
        using var operador = await api.ComoAsync("Operador");

        var vazio = await operador.PostAsJsonAsync(Rota, new SincronizacaoRequest([]));
        var ausente = await operador.PostAsJsonAsync(Rota, new { });

        vazio.AssertStatus(HttpStatusCode.BadRequest);
        ausente.AssertStatus(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Lote_maior_que_o_limite_devolve_400()
    {
        using var operador = await api.ComoAsync("Operador");
        var itens = Enumerable.Range(0, SincronizacaoRequestValidator.TamanhoMaximoDoLote + 1).Select(_ => Item("Inicio", 1)).ToList();

        var resposta = await operador.PostAsJsonAsync(Rota, new SincronizacaoRequest(itens));

        resposta.AssertStatus(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Item_nulo_no_lote_e_rejeitado_sem_derrubar_o_lote()
    {
        using var operador = await api.ComoAsync("Operador");

        var resposta = await operador.PostAsJsonAsync(Rota, new { itens = new object?[] { null } });

        resposta.AssertStatus(HttpStatusCode.OK);
        var corpo = (await resposta.Content.ReadFromJsonAsync<SincronizacaoResponse>())!;
        Assert.Equal("Rejeitado", Assert.Single(corpo.Resultados).Status);
    }
}
