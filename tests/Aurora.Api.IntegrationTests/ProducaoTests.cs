using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Aurora.Application.Comum;
using Aurora.Application.Estoque;
using Aurora.Application.Pedidos;
using Aurora.Application.Producao;

namespace Aurora.Api.IntegrationTests;

[Collection("api")]
public sealed partial class ProducaoTests(ApiFactory api)
{
    [GeneratedRegex(@"^OP-\d{4}-\d{5}$")]
    private static partial Regex FormatoDoNumero();

    private static Task<HttpResponseMessage> IniciarAsync(HttpClient cliente, int etapaId, Guid? requisicao = null) =>
        cliente.PostAsJsonAsync($"/api/v1/etapas/{etapaId}/iniciar", CenarioDeProducao.NovoApontamento(requisicao));

    private static Task<HttpResponseMessage> FinalizarAsync(HttpClient cliente, int etapaId, Guid? requisicao = null) =>
        cliente.PostAsJsonAsync($"/api/v1/etapas/{etapaId}/finalizar", CenarioDeProducao.NovoApontamento(requisicao));

    private static async Task<EtapaDto> LerEtapaAsync(HttpResponseMessage resposta)
    {
        resposta.AssertStatus(HttpStatusCode.OK);
        return (await resposta.Content.ReadFromJsonAsync<EtapaDto>())!;
    }

    [Fact]
    public async Task Confirmar_pedido_sem_estoque_de_produto_gera_uma_op_com_as_etapas_do_roteiro()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");

        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 5);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);

        Assert.Equal("EmProducao", pedido.Status);
        Assert.Matches(FormatoDoNumero(), ordem.Ordem.Numero);
        Assert.Equal("Planejada", ordem.Ordem.Status);
        Assert.Equal(5, ordem.Ordem.Quantidade);
        Assert.Equal(pedido.Numero, ordem.Ordem.PedidoNumero);
        Assert.Equal(cenario.Produto.Codigo, ordem.Ordem.ProdutoCodigo);
        Assert.Equal(cenario.Cliente.Nome, ordem.ClienteNome);
        Assert.Equal(pedido.PrazoEntrega.AddDays(-2), ordem.Ordem.PrevisaoConclusao);
        Assert.NotEqual(Guid.Empty, ordem.CodigoQr);
        Assert.Equal([1, 2, 3], ordem.Etapas.Select(e => (int)e.Sequencia));
        Assert.Equal(
            [CenarioDeProducao.Corte, CenarioDeProducao.MontagemDaEstrutura, CenarioDeProducao.Qualidade],
            ordem.Etapas.Select(e => e.EstacaoId));
        Assert.All(ordem.Etapas, e => Assert.Equal("Pendente", e.Status));
        Assert.Equal("Corte", ordem.Ordem.EtapaAtual!.EstacaoNome);
        Assert.Equal(0, ordem.Ordem.EtapasConcluidas);
        Assert.Equal(3, ordem.Ordem.TotalDeEtapas);
    }

    [Fact]
    public async Task Confirmar_nao_baixa_materia_prima_porque_a_baixa_acontece_ao_iniciar_a_etapa()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 100m);
        using var vendedor = await api.ComoAsync("Vendedor");

        await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 5);

        Assert.Equal(100m, await cenario.SaldoAsync());
    }

    [Fact]
    public async Task Confirmar_pedido_ja_confirmado_devolve_409_e_nao_gera_outra_op()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 2);

        var repetida = await vendedor.PostAsync($"/api/v1/pedidos/{pedido.Id}/confirmar", content: null);

        repetida.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("STATUS_INVALIDO", await repetida.CodigoAsync());
        var ordens = await gerente.GetFromJsonAsync<Pagina<OrdemProducaoResumoDto>>($"/api/v1/ordens-producao?pedidoId={pedido.Id}");
        Assert.Single(ordens!.Items);
    }

    [Fact]
    public async Task Confirmar_pedido_cancelado_ou_inexistente_devolve_o_erro_certo()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        var pedido = await cenario.PedidoAbertoAsync(vendedor, quantidade: 1);
        (await vendedor.PostAsync($"/api/v1/pedidos/{pedido.Id}/cancelar", content: null)).AssertStatus(HttpStatusCode.OK);

        var cancelado = await vendedor.PostAsync($"/api/v1/pedidos/{pedido.Id}/confirmar", content: null);
        var inexistente = await vendedor.PostAsync("/api/v1/pedidos/2147000000/confirmar", content: null);

        cancelado.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("STATUS_INVALIDO", await cancelado.CodigoAsync());
        inexistente.AssertStatus(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Confirmar_pedido_de_produto_sem_roteiro_devolve_409_e_o_pedido_continua_aberto()
    {
        using var admin = await api.ComoAdministradorAsync();
        using var vendedor = await api.ComoAsync("Vendedor");
        var cenario = await CenarioDeProducao.CriarAsync(api);
        var semRoteiro = await RoteiroTests.CriarProdutoAsync(admin);
        var pedido = await Criar(vendedor, cenario, semRoteiro.Id);

        var resposta = await vendedor.PostAsync($"/api/v1/pedidos/{pedido.Id}/confirmar", content: null);

        resposta.AssertStatus(HttpStatusCode.Conflict);
        var atual = await vendedor.GetFromJsonAsync<PedidoDto>($"/api/v1/pedidos/{pedido.Id}");
        Assert.Equal("Aberto", atual!.Status);
    }

    [Fact]
    public async Task Iniciar_a_primeira_etapa_baixa_o_estoque_conforme_a_ficha_tecnica_e_registra_o_movimento()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 100m, consumoPorUnidade: 2m);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 5);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        var corte = ordem.Etapas[0];

        var etapa = await LerEtapaAsync(await IniciarAsync(operador, corte.Id));

        Assert.Equal("EmAndamento", etapa.Status);
        Assert.NotNull(etapa.IniciadaEm);
        Assert.NotNull(etapa.OperadorId);
        Assert.Equal(90m, await cenario.SaldoAsync());
        var extrato = await almoxarife.GetFromJsonAsync<Pagina<MovimentoEstoqueDto>>(
            $"/api/v1/estoque/movimentos?materiaPrimaId={cenario.Materia.Id}&origem=Producao");
        var movimento = Assert.Single(extrato!.Items);
        Assert.Equal("Saida", movimento.Tipo);
        Assert.Equal(-10m, movimento.Quantidade);
        Assert.Equal(corte.Id, movimento.OrigemId);
        var depois = await gerente.GetFromJsonAsync<OrdemProducaoDto>($"/api/v1/ordens-producao/{ordem.Ordem.Id}");
        Assert.Equal("EmAndamento", depois!.Ordem.Status);
        Assert.Equal("Inicio", Assert.Single(depois.Apontamentos).Tipo);
    }

    [Fact]
    public async Task Reenviar_o_mesmo_client_request_id_nao_baixa_o_estoque_de_novo()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 100m, consumoPorUnidade: 2m);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 5);
        var corte = (await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id)).Etapas[0];
        var requisicao = Guid.NewGuid();

        var primeira = await LerEtapaAsync(await IniciarAsync(operador, corte.Id, requisicao));
        var repetida = await LerEtapaAsync(await IniciarAsync(operador, corte.Id, requisicao));

        Assert.Equal("EmAndamento", primeira.Status);
        Assert.Equal("EmAndamento", repetida.Status);
        Assert.Equal(90m, await cenario.SaldoAsync());
    }

    [Fact]
    public async Task Iniciar_com_o_mesmo_identificador_em_paralelo_baixa_o_estoque_uma_vez_so()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 100m, consumoPorUnidade: 2m);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 5);
        var corte = (await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id)).Etapas[0];
        var requisicao = Guid.NewGuid();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => IniciarAsync(operador, corte.Id, requisicao)));

        Assert.All(respostas, r => r.AssertStatus(HttpStatusCode.OK));
        Assert.Equal(90m, await cenario.SaldoAsync());
    }

    [Fact]
    public async Task Iniciar_sem_saldo_suficiente_devolve_409_rn04_e_a_etapa_continua_pendente()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 5m, consumoPorUnidade: 2m);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 5);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);

        var resposta = await IniciarAsync(operador, ordem.Etapas[0].Id);

        resposta.AssertStatus(HttpStatusCode.Conflict);
        var problema = await resposta.ProblemaAsync();
        Assert.Equal("RN04", problema.GetProperty("code").GetString());
        Assert.Contains("Insumo de Produção", problema.GetProperty("detail").GetString());
        Assert.Equal(5m, await cenario.SaldoAsync());
        var depois = await gerente.GetFromJsonAsync<OrdemProducaoDto>($"/api/v1/ordens-producao/{ordem.Ordem.Id}");
        Assert.Equal("Pendente", depois!.Etapas[0].Status);
        Assert.Equal("Planejada", depois.Ordem.Status);
        Assert.Empty(depois.Apontamentos);
    }

    [Fact]
    public async Task Iniciar_com_o_saldo_exatamente_igual_ao_consumo_funciona_e_zera_o_estoque()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 10m, consumoPorUnidade: 2m);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 5);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);

        (await IniciarAsync(operador, ordem.Etapas[0].Id)).AssertStatus(HttpStatusCode.OK);

        Assert.Equal(0m, await cenario.SaldoAsync());
    }

    [Fact]
    public async Task Iniciar_etapa_antes_de_concluir_a_anterior_devolve_409_rn05()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 2);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);

        var pulou = await IniciarAsync(operador, ordem.Etapas[1].Id);
        (await IniciarAsync(operador, ordem.Etapas[0].Id)).AssertStatus(HttpStatusCode.OK);
        var aindaEmAndamento = await IniciarAsync(operador, ordem.Etapas[1].Id);

        pulou.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("RN05", await pulou.CodigoAsync());
        aindaEmAndamento.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("RN05", await aindaEmAndamento.CodigoAsync());
    }

    [Fact]
    public async Task Iniciar_etapa_que_ja_esta_em_andamento_com_outro_identificador_devolve_409_status_invalido()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 100m);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 2);
        var corte = (await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id)).Etapas[0];
        (await IniciarAsync(operador, corte.Id)).AssertStatus(HttpStatusCode.OK);

        var segunda = await IniciarAsync(operador, corte.Id);

        segunda.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("STATUS_INVALIDO", await segunda.CodigoAsync());
        Assert.Equal(96m, await cenario.SaldoAsync());
    }

    [Fact]
    public async Task Finalizar_conclui_a_etapa_libera_a_proxima_e_atualiza_o_quadro()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 2);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);

        (await IniciarAsync(operador, ordem.Etapas[0].Id)).AssertStatus(HttpStatusCode.OK);
        var concluida = await LerEtapaAsync(await FinalizarAsync(operador, ordem.Etapas[0].Id));
        var proxima = await LerEtapaAsync(await IniciarAsync(operador, ordem.Etapas[1].Id));

        Assert.Equal("Concluida", concluida.Status);
        Assert.NotNull(concluida.ConcluidaEm);
        Assert.Equal("EmAndamento", proxima.Status);
        var depois = await gerente.GetFromJsonAsync<OrdemProducaoDto>($"/api/v1/ordens-producao/{ordem.Ordem.Id}");
        Assert.Equal(1, depois!.Ordem.EtapasConcluidas);
        Assert.Equal("Montagem da Estrutura", depois.Ordem.EtapaAtual!.EstacaoNome);
        Assert.Equal(["Inicio", "Fim", "Inicio"], depois.Apontamentos.Select(a => a.Tipo));
    }

    [Fact]
    public async Task Finalizar_etapa_pendente_ou_ja_concluida_devolve_409()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 2);
        var corte = (await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id)).Etapas[0];

        var pendente = await FinalizarAsync(operador, corte.Id);
        (await IniciarAsync(operador, corte.Id)).AssertStatus(HttpStatusCode.OK);
        (await FinalizarAsync(operador, corte.Id)).AssertStatus(HttpStatusCode.OK);
        var concluida = await FinalizarAsync(operador, corte.Id);

        Assert.All([pendente, concluida], r => r.AssertStatus(HttpStatusCode.Conflict));
        Assert.Equal("STATUS_INVALIDO", await pendente.CodigoAsync());
    }

    [Fact]
    public async Task Finalizar_com_o_mesmo_identificador_e_idempotente()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 2);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        (await IniciarAsync(operador, ordem.Etapas[0].Id)).AssertStatus(HttpStatusCode.OK);
        var requisicao = Guid.NewGuid();

        (await FinalizarAsync(operador, ordem.Etapas[0].Id, requisicao)).AssertStatus(HttpStatusCode.OK);
        (await FinalizarAsync(operador, ordem.Etapas[0].Id, requisicao)).AssertStatus(HttpStatusCode.OK);

        var depois = await gerente.GetFromJsonAsync<OrdemProducaoDto>($"/api/v1/ordens-producao/{ordem.Ordem.Id}");
        Assert.Equal(1, depois!.Apontamentos.Count(a => a.Tipo == "Fim"));
    }

    [Fact]
    public async Task A_etapa_de_qualidade_nao_e_finalizada_pelo_operador_e_so_por_inspecao()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        foreach (var etapa in ordem.Etapas.Take(2))
        {
            (await IniciarAsync(operador, etapa.Id)).AssertStatus(HttpStatusCode.OK);
            (await FinalizarAsync(operador, etapa.Id)).AssertStatus(HttpStatusCode.OK);
        }

        (await IniciarAsync(operador, ordem.Etapas[2].Id)).AssertStatus(HttpStatusCode.OK);
        var finalizar = await FinalizarAsync(operador, ordem.Etapas[2].Id);

        finalizar.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("STATUS_INVALIDO", await finalizar.CodigoAsync());
    }

    [Fact]
    public async Task Etapa_inexistente_devolve_404()
    {
        using var operador = await api.ComoAsync("Operador");

        var iniciar = await IniciarAsync(operador, 2147000000);
        var finalizar = await FinalizarAsync(operador, 2147000000);
        var perda = await operador.PostAsJsonAsync("/api/v1/etapas/2147000000/perdas", new PerdaRequest("Refugo", 1, "Motivo"));

        Assert.All([iniciar, finalizar, perda], r => r.AssertStatus(HttpStatusCode.NotFound));
        Assert.Equal("ETAPA_NAO_ENCONTRADA", await iniciar.CodigoAsync());
    }

    [Fact]
    public async Task Apontamento_invalido_devolve_400_sem_tocar_no_banco()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var corte = (await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id)).Etapas[0];

        var semIdentificador = await operador.PostAsJsonAsync(
            $"/api/v1/etapas/{corte.Id}/iniciar", new ApontamentoRequest(Guid.Empty, DateTimeOffset.UtcNow));
        var noFuturo = await operador.PostAsJsonAsync(
            $"/api/v1/etapas/{corte.Id}/iniciar", new ApontamentoRequest(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(2)));

        Assert.All([semIdentificador, noFuturo], r => r.AssertStatus(HttpStatusCode.BadRequest));
        Assert.Equal(100m, await cenario.SaldoAsync());
    }

    [Fact]
    public async Task Perda_em_etapa_em_andamento_e_registrada_e_aparece_no_detalhe_da_op()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 5);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        (await IniciarAsync(operador, ordem.Etapas[0].Id)).AssertStatus(HttpStatusCode.OK);

        var resposta = await operador.PostAsJsonAsync(
            $"/api/v1/etapas/{ordem.Etapas[0].Id}/perdas", new PerdaRequest("refugo", 2, "Madeira rachada no corte"));

        resposta.AssertStatus(HttpStatusCode.Created);
        var perda = (await resposta.Content.ReadFromJsonAsync<PerdaDto>())!;
        Assert.Equal("Refugo", perda.Tipo);
        Assert.Equal(2, perda.Quantidade);
        Assert.Equal("Corte", perda.EstacaoNome);
        var detalhe = await gerente.GetFromJsonAsync<OrdemProducaoDto>($"/api/v1/ordens-producao/{ordem.Ordem.Id}");
        var lida = Assert.Single(detalhe!.Perdas);
        Assert.Equal("Madeira rachada no corte", lida.Motivo);
        Assert.Equal(perda.Id, lida.Id);
        Assert.False(string.IsNullOrWhiteSpace(lida.UsuarioNome));
    }

    [Fact]
    public async Task Perda_em_etapa_pendente_devolve_409_e_acima_da_quantidade_devolve_422()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 3);
        var corte = (await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id)).Etapas[0];

        var pendente = await operador.PostAsJsonAsync($"/api/v1/etapas/{corte.Id}/perdas", new PerdaRequest("Refugo", 1, "Motivo"));
        (await IniciarAsync(operador, corte.Id)).AssertStatus(HttpStatusCode.OK);
        var acima = await operador.PostAsJsonAsync($"/api/v1/etapas/{corte.Id}/perdas", new PerdaRequest("Refugo", 4, "Motivo"));
        var invalida = await operador.PostAsJsonAsync($"/api/v1/etapas/{corte.Id}/perdas", new PerdaRequest("Quebra", 1, "Motivo"));

        pendente.AssertStatus(HttpStatusCode.Conflict);
        acima.AssertStatus(HttpStatusCode.UnprocessableEntity);
        Assert.Equal("PERDA_ACIMA_DA_QUANTIDADE", await acima.CodigoAsync());
        invalida.AssertStatus(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cancelar_pedido_confirmado_cancela_as_ops_e_a_etapa_ja_iniciada_nao_devolve_o_material()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api, saldo: 100m, consumoPorUnidade: 2m);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 5);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        (await IniciarAsync(operador, ordem.Etapas[0].Id)).AssertStatus(HttpStatusCode.OK);

        var cancelado = await vendedor.PostAsync($"/api/v1/pedidos/{pedido.Id}/cancelar", content: null);

        cancelado.AssertStatus(HttpStatusCode.OK);
        var depois = await gerente.GetFromJsonAsync<OrdemProducaoDto>($"/api/v1/ordens-producao/{ordem.Ordem.Id}");
        Assert.Equal("Cancelada", depois!.Ordem.Status);
        Assert.Equal(90m, await cenario.SaldoAsync());
    }

    [Fact]
    public async Task Listar_ops_filtra_por_status_estacao_atual_e_atraso()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);

        async Task<IReadOnlyList<int>> IdsAsync(string consulta) =>
            (await gerente.GetFromJsonAsync<Pagina<OrdemProducaoResumoDto>>($"/api/v1/ordens-producao?pedidoId={pedido.Id}&{consulta}"))!
                .Items.Select(o => o.Id).ToList();

        var noCorte = await IdsAsync($"estacaoId={CenarioDeProducao.Corte}");
        var naMontagem = await IdsAsync($"estacaoId={CenarioDeProducao.MontagemDaEstrutura}");
        var planejadas = await IdsAsync("status=Planejada");
        var emAndamento = await IdsAsync("status=EmAndamento");
        var atrasadas = await IdsAsync("atrasadas=true");
        (await IniciarAsync(operador, ordem.Etapas[0].Id)).AssertStatus(HttpStatusCode.OK);
        (await FinalizarAsync(operador, ordem.Etapas[0].Id)).AssertStatus(HttpStatusCode.OK);
        var agoraNaMontagem = await IdsAsync($"estacaoId={CenarioDeProducao.MontagemDaEstrutura}");
        var agoraNoCorte = await IdsAsync($"estacaoId={CenarioDeProducao.Corte}");

        Assert.Equal([ordem.Ordem.Id], noCorte);
        Assert.Empty(naMontagem);
        Assert.Equal([ordem.Ordem.Id], planejadas);
        Assert.Empty(emAndamento);
        Assert.Empty(atrasadas);
        Assert.Equal([ordem.Ordem.Id], agoraNaMontagem);
        Assert.Empty(agoraNoCorte);
    }

    [Fact]
    public async Task Quadro_mostra_a_op_na_coluna_da_estacao_atual_com_o_tempo_na_etapa()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        (await IniciarAsync(operador, ordem.Etapas[0].Id)).AssertStatus(HttpStatusCode.OK);

        var quadro = (await gerente.GetFromJsonAsync<QuadroDeProducaoDto>("/api/v1/ordens-producao/quadro"))!;

        Assert.Equal(
            ["Corte", "Montagem da Estrutura", "Estofaria", "Montagem Final", "Qualidade"],
            quadro.Colunas.Select(c => c.EstacaoNome));
        var cartao = Assert.Single(quadro.Colunas.Single(c => c.EstacaoId == CenarioDeProducao.Corte).Ordens, o => o.Id == ordem.Ordem.Id);
        Assert.Equal("EmAndamento", cartao.EtapaAtual!.Status);
        Assert.InRange(cartao.EtapaAtual.MinutosNaEtapa!.Value, 0, 5);
        Assert.False(cartao.Atrasada);
        Assert.DoesNotContain(
            quadro.Colunas.Where(c => c.EstacaoId != CenarioDeProducao.Corte).SelectMany(c => c.Ordens), o => o.Id == ordem.Ordem.Id);
    }

    private static async Task<PedidoDto> Criar(HttpClient vendedor, CenarioDeProducao cenario, int produtoId)
    {
        var resposta = await vendedor.PostAsJsonAsync(
            "/api/v1/pedidos",
            new PedidoRequest(cenario.Cliente.Id, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30), null, [new PedidoItemRequest(produtoId, 1)]));
        resposta.AssertStatus(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<PedidoDto>())!;
    }
}
