using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Comum;
using Aurora.Application.Estoque;
using Aurora.Application.Pedidos;
using Aurora.Application.Producao;
using Aurora.Application.Qualidade;

namespace Aurora.Api.IntegrationTests;

/// <summary>Inspeção de qualidade (RF18, RN06): aprovar conclui a OP e libera o pedido; reprovar reabre as etapas.</summary>
[Collection("api")]
public class InspecaoTests(ApiFactory api)
{
    private static Task<HttpResponseMessage> InspecionarAsync(
        HttpClient inspetor, int ordemId, string resultado, byte? retrabalho = null, string? observacao = null) =>
        inspetor.PostAsJsonAsync(
            $"/api/v1/ordens-producao/{ordemId}/inspecoes", new InspecaoRequest(resultado, retrabalho, observacao));

    private static async Task<InspecaoDto> LerAsync(HttpResponseMessage resposta)
    {
        resposta.AssertStatus(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<InspecaoDto>())!;
    }

    private static async Task<SaldoDeProdutoDto> SaldoDoProdutoAsync(HttpClient almoxarife, string codigo) =>
        (await almoxarife.GetFromJsonAsync<Pagina<SaldoDeProdutoDto>>($"/api/v1/estoque/produtos?busca={codigo}"))!
        .Items.Single(p => p.Codigo == codigo);

    [Fact]
    public async Task Aprovar_conclui_a_op_da_entrada_do_produto_acabado_e_deixa_o_pedido_pronto()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        using var inspetor = await api.ComoAsync("Inspetor");
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 3);
        var antes = await CenarioDeProducao.AvancarAteAQualidadeAsync(gerente, operador, pedido.Id);

        var inspecao = await LerAsync(await InspecionarAsync(inspetor, antes.Ordem.Id, "Aprovada", observacao: "  Sem defeitos  "));

        Assert.Equal("Aprovada", inspecao.Resultado);
        Assert.Equal(antes.Ordem.Numero, inspecao.NumeroOp);
        Assert.Equal(pedido.Numero, inspecao.PedidoNumero);
        Assert.Equal("Sem defeitos", inspecao.Observacao);
        Assert.Null(inspecao.EstacaoRetrabalhoId);
        Assert.Equal("Concluida", inspecao.StatusOrdem);
        Assert.Equal("Pronto", inspecao.StatusPedido);
        Assert.False(string.IsNullOrEmpty(inspecao.InspetorNome));

        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        Assert.Equal("Concluida", ordem.Ordem.Status);
        Assert.All(ordem.Etapas, e => Assert.Equal("Concluida", e.Status));
        var saldo = await SaldoDoProdutoAsync(almoxarife, cenario.Produto.Codigo);
        Assert.Equal(3, saldo.Quantidade);
        Assert.Equal(3, saldo.Reservado);
        Assert.Equal(0, saldo.Disponivel);
        var pedidoDepois = await vendedor.GetFromJsonAsync<PedidoDto>($"/api/v1/pedidos/{pedido.Id}");
        Assert.Equal("Pronto", pedidoDepois!.Status);
    }

    [Fact]
    public async Task Pedido_com_duas_ops_so_fica_pronto_quando_a_ultima_for_aprovada()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var admin = await api.ComoAdministradorAsync();
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        using var inspetor = await api.ComoAsync("Inspetor");

        // Dois itens do mesmo pedido, de produtos diferentes, geram duas OPs
        var segundo = await CenarioDeProducao.NovoProdutoComOMesmoRoteiroAsync(admin);
        var pedido = await cenario.PedidoComDoisProdutosConfirmadoAsync(vendedor, segundo);
        var ordens = (await gerente.GetFromJsonAsync<Pagina<OrdemProducaoResumoDto>>($"/api/v1/ordens-producao?pedidoId={pedido.Id}"))!.Items;
        Assert.Equal(2, ordens.Count);

        string statusDoPedido = pedido.Status;
        foreach (var op in ordens)
        {
            var detalhe = (await gerente.GetFromJsonAsync<OrdemProducaoDto>($"/api/v1/ordens-producao/{op.Id}"))!;
            foreach (var etapa in detalhe.Etapas.Where(e => e.EstacaoId != CenarioDeProducao.Qualidade).OrderBy(e => e.Sequencia))
            {
                (await operador.PostAsJsonAsync($"/api/v1/etapas/{etapa.Id}/iniciar", CenarioDeProducao.NovoApontamento())).AssertStatus(HttpStatusCode.OK);
                (await operador.PostAsJsonAsync($"/api/v1/etapas/{etapa.Id}/finalizar", CenarioDeProducao.NovoApontamento())).AssertStatus(HttpStatusCode.OK);
            }

            var qualidade = detalhe.Etapas.Single(e => e.EstacaoId == CenarioDeProducao.Qualidade);
            (await operador.PostAsJsonAsync($"/api/v1/etapas/{qualidade.Id}/iniciar", CenarioDeProducao.NovoApontamento())).AssertStatus(HttpStatusCode.OK);
            statusDoPedido = (await LerAsync(await InspecionarAsync(inspetor, op.Id, "Aprovada"))).StatusPedido;
            if (op.Id != ordens[^1].Id)
            {
                Assert.Equal("EmProducao", statusDoPedido);
            }
        }

        Assert.Equal("Pronto", statusDoPedido);
    }

    [Fact]
    public async Task Reprovar_reabre_as_etapas_a_partir_do_retrabalho_registra_a_perda_e_a_op_volta_a_produzir()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        using var inspetor = await api.ComoAsync("Inspetor");
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 3);
        var antes = await CenarioDeProducao.AvancarAteAQualidadeAsync(gerente, operador, pedido.Id);

        var reprovada = await LerAsync(await InspecionarAsync(
            inspetor, antes.Ordem.Id, "Reprovada", CenarioDeProducao.MontagemDaEstrutura, "Estrutura torta"));

        Assert.Equal("Reprovada", reprovada.Resultado);
        Assert.Equal(CenarioDeProducao.MontagemDaEstrutura, reprovada.EstacaoRetrabalhoId);
        Assert.Equal("Montagem da Estrutura", reprovada.EstacaoRetrabalhoNome);
        Assert.Equal("EmAndamento", reprovada.StatusOrdem);
        Assert.Equal("EmProducao", reprovada.StatusPedido);
        var reaberta = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        Assert.Equal(["Concluida", "Pendente", "Pendente"], reaberta.Etapas.OrderBy(e => e.Sequencia).Select(e => e.Status));
        var perda = Assert.Single(reaberta.Perdas);
        Assert.Equal("Retrabalho", perda.Tipo);
        Assert.Equal(3, perda.Quantidade);
        Assert.Equal("Estrutura torta", perda.Motivo);
        Assert.Equal(0, (await SaldoDoProdutoAsync(almoxarife, cenario.Produto.Codigo)).Quantidade);

        // Refaz o que foi reaberto e aprova
        await CenarioDeProducao.AvancarAteAQualidadeAsync(gerente, operador, pedido.Id);
        var aprovada = await LerAsync(await InspecionarAsync(inspetor, antes.Ordem.Id, "Aprovada"));
        Assert.Equal("Pronto", aprovada.StatusPedido);
        Assert.Equal(3, (await SaldoDoProdutoAsync(almoxarife, cenario.Produto.Codigo)).Quantidade);
    }

    [Fact]
    public async Task Reprovar_sem_estacao_de_retrabalho_devolve_422_RN06_e_nada_muda()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        using var inspetor = await api.ComoAsync("Inspetor");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var antes = await CenarioDeProducao.AvancarAteAQualidadeAsync(gerente, operador, pedido.Id);

        var resposta = await InspecionarAsync(inspetor, antes.Ordem.Id, "Reprovada");

        resposta.AssertStatus(HttpStatusCode.UnprocessableEntity);
        Assert.Equal("RN06", await resposta.CodigoAsync());
        var historico = await gerente.GetFromJsonAsync<Pagina<InspecaoDto>>($"/api/v1/inspecoes?ordemProducaoId={antes.Ordem.Id}");
        Assert.Empty(historico!.Items);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        Assert.Equal("EmAndamento", ordem.Etapas.Single(e => e.EstacaoId == CenarioDeProducao.Qualidade).Status);
    }

    [Theory]
    [InlineData(CenarioDeProducao.Qualidade)] // a própria Qualidade não é retrabalho
    [InlineData((byte)4)] // Montagem Final: não está no roteiro deste produto
    public async Task Reprovar_com_estacao_de_retrabalho_invalida_devolve_422_RN06(byte estacao)
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        using var inspetor = await api.ComoAsync("Inspetor");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var antes = await CenarioDeProducao.AvancarAteAQualidadeAsync(gerente, operador, pedido.Id);

        var resposta = await InspecionarAsync(inspetor, antes.Ordem.Id, "Reprovada", estacao);

        resposta.AssertStatus(HttpStatusCode.UnprocessableEntity);
        Assert.Equal("RN06", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Aprovar_ignora_a_estacao_de_retrabalho_informada()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        using var inspetor = await api.ComoAsync("Inspetor");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var antes = await CenarioDeProducao.AvancarAteAQualidadeAsync(gerente, operador, pedido.Id);

        var inspecao = await LerAsync(await InspecionarAsync(inspetor, antes.Ordem.Id, "Aprovada", CenarioDeProducao.Corte));

        Assert.Null(inspecao.EstacaoRetrabalhoId);
    }

    [Theory]
    [InlineData("Talvez")]
    [InlineData("1")]
    [InlineData("")]
    public async Task Resultado_invalido_devolve_400(string resultado)
    {
        using var inspetor = await api.ComoAsync("Inspetor");

        var resposta = await InspecionarAsync(inspetor, 2147000000, resultado);

        resposta.AssertStatus(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Observacao_acima_de_300_caracteres_devolve_400()
    {
        using var inspetor = await api.ComoAsync("Inspetor");

        var resposta = await InspecionarAsync(inspetor, 2147000000, "Aprovada", observacao: new string('x', 301));

        resposta.AssertStatus(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Inspecionar_op_inexistente_devolve_404()
    {
        using var inspetor = await api.ComoAsync("Inspetor");

        var resposta = await InspecionarAsync(inspetor, 2147000000, "Aprovada");

        resposta.AssertStatus(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Inspecionar_op_cuja_qualidade_nao_comecou_devolve_409()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var inspetor = await api.ComoAsync("Inspetor");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);

        var resposta = await InspecionarAsync(inspetor, ordem.Ordem.Id, "Aprovada");

        resposta.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("STATUS_INVALIDO", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Inspecionar_duas_vezes_a_mesma_op_aprovada_devolve_409_e_nao_duplica_o_estoque()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        using var inspetor = await api.ComoAsync("Inspetor");
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 2);
        var antes = await CenarioDeProducao.AvancarAteAQualidadeAsync(gerente, operador, pedido.Id);
        await LerAsync(await InspecionarAsync(inspetor, antes.Ordem.Id, "Aprovada"));

        var repetida = await InspecionarAsync(inspetor, antes.Ordem.Id, "Aprovada");

        repetida.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("STATUS_INVALIDO", await repetida.CodigoAsync());
        Assert.Equal(2, (await SaldoDoProdutoAsync(almoxarife, cenario.Produto.Codigo)).Quantidade);
    }

    [Fact]
    public async Task Historico_lista_da_mais_recente_para_a_mais_antiga_e_filtra_por_op_resultado_e_periodo()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        using var inspetor = await api.ComoAsync("Inspetor");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var antes = await CenarioDeProducao.AvancarAteAQualidadeAsync(gerente, operador, pedido.Id);
        await LerAsync(await InspecionarAsync(inspetor, antes.Ordem.Id, "Reprovada", CenarioDeProducao.MontagemDaEstrutura));
        await CenarioDeProducao.AvancarAteAQualidadeAsync(gerente, operador, pedido.Id);
        await LerAsync(await InspecionarAsync(inspetor, antes.Ordem.Id, "Aprovada"));
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var rota = $"/api/v1/inspecoes?ordemProducaoId={antes.Ordem.Id}";

        var todas = await gerente.GetFromJsonAsync<Pagina<InspecaoDto>>(rota);
        var reprovadas = await gerente.GetFromJsonAsync<Pagina<InspecaoDto>>(rota + "&resultado=Reprovada");
        var doFuturo = await gerente.GetFromJsonAsync<Pagina<InspecaoDto>>(rota + $"&de={hoje.AddDays(2):yyyy-MM-dd}");
        var deHoje = await gerente.GetFromJsonAsync<Pagina<InspecaoDto>>(rota + $"&de={hoje.AddDays(-1):yyyy-MM-dd}&ate={hoje.AddDays(1):yyyy-MM-dd}");

        Assert.Equal(["Aprovada", "Reprovada"], todas!.Items.Select(i => i.Resultado));
        Assert.Equal(2, todas.Total);
        Assert.Equal("Reprovada", Assert.Single(reprovadas!.Items).Resultado);
        Assert.Empty(doFuturo!.Items);
        Assert.Equal(2, deHoje!.Total);
    }
}
