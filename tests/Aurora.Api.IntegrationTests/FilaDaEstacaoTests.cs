using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Producao;

namespace Aurora.Api.IntegrationTests;

/// <summary>Leitura do QR da etiqueta e fila de etapas da estação (RF11), usadas pelo app do operador.</summary>
[Collection("api")]
public class FilaDaEstacaoTests(ApiFactory api)
{
    private static async Task<IReadOnlyList<EtapaDaEstacaoDto>> FilaAsync(HttpClient cliente, byte estacaoId) =>
        (await cliente.GetFromJsonAsync<List<EtapaDaEstacaoDto>>($"/api/v1/etapas/minhas?estacaoId={estacaoId}"))!;

    [Fact]
    public async Task Qr_da_etiqueta_localiza_a_op_com_etapas_e_dados_do_pedido()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 2);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);

        var resposta = await operador.GetAsync($"/api/v1/ordens-producao/qr/{ordem.CodigoQr}");

        resposta.AssertStatus(HttpStatusCode.OK);
        var lida = (await resposta.Content.ReadFromJsonAsync<OrdemProducaoDto>())!;
        Assert.Equal(ordem.Ordem.Id, lida.Ordem.Id);
        Assert.Equal(ordem.CodigoQr, lida.CodigoQr);
        Assert.Equal(pedido.Numero, lida.Ordem.PedidoNumero);
        Assert.Equal(3, lida.Etapas.Count);
    }

    [Fact]
    public async Task Qr_desconhecido_devolve_404()
    {
        using var operador = await api.ComoAsync("Operador");

        var resposta = await operador.GetAsync($"/api/v1/ordens-producao/qr/{Guid.NewGuid()}");

        resposta.AssertStatus(HttpStatusCode.NotFound);
        Assert.Equal("ORDEM_PRODUCAO_NAO_ENCONTRADA", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Fila_traz_a_etapa_liberada_na_primeira_estacao_e_a_travada_na_seguinte()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 3);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);

        var corte = (await FilaAsync(operador, CenarioDeProducao.Corte)).Single(e => e.OrdemProducaoId == ordem.Ordem.Id);
        var montagem = (await FilaAsync(operador, CenarioDeProducao.MontagemDaEstrutura)).Single(e => e.OrdemProducaoId == ordem.Ordem.Id);

        Assert.True(corte.Liberada);
        Assert.Equal("Pendente", corte.Status);
        Assert.Equal(ordem.Ordem.Numero, corte.NumeroOp);
        Assert.Equal(ordem.CodigoQr, corte.CodigoQr);
        Assert.Equal(pedido.Numero, corte.PedidoNumero);
        Assert.Equal(cenario.Produto.Codigo, corte.ProdutoCodigo);
        Assert.Equal(3, corte.Quantidade);
        Assert.Equal("Corte", corte.EstacaoNome);
        Assert.False(montagem.Liberada);
    }

    [Fact]
    public async Task Fila_acompanha_o_andamento_e_a_etapa_concluida_sai_da_lista()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        var corte = ordem.Etapas.Single(e => e.EstacaoId == CenarioDeProducao.Corte);

        (await operador.PostAsJsonAsync($"/api/v1/etapas/{corte.Id}/iniciar", CenarioDeProducao.NovoApontamento())).AssertStatus(HttpStatusCode.OK);
        var emAndamento = (await FilaAsync(operador, CenarioDeProducao.Corte)).Single(e => e.OrdemProducaoId == ordem.Ordem.Id);
        (await operador.PostAsJsonAsync($"/api/v1/etapas/{corte.Id}/finalizar", CenarioDeProducao.NovoApontamento())).AssertStatus(HttpStatusCode.OK);
        var depoisDoCorte = await FilaAsync(operador, CenarioDeProducao.Corte);
        var montagem = (await FilaAsync(operador, CenarioDeProducao.MontagemDaEstrutura)).Single(e => e.OrdemProducaoId == ordem.Ordem.Id);

        Assert.Equal("EmAndamento", emAndamento.Status);
        Assert.NotNull(emAndamento.IniciadaEm);
        Assert.NotNull(emAndamento.OperadorId);
        Assert.DoesNotContain(depoisDoCorte, e => e.OrdemProducaoId == ordem.Ordem.Id);
        Assert.True(montagem.Liberada);
    }

    [Fact]
    public async Task Fila_poe_as_etapas_em_andamento_antes_das_pendentes()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var primeiro = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var segundo = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var ordemDoSegundo = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, segundo.Id);
        var corteDoSegundo = ordemDoSegundo.Etapas.Single(e => e.EstacaoId == CenarioDeProducao.Corte);
        (await operador.PostAsJsonAsync($"/api/v1/etapas/{corteDoSegundo.Id}/iniciar", CenarioDeProducao.NovoApontamento()))
            .AssertStatus(HttpStatusCode.OK);
        var ordemDoPrimeiro = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, primeiro.Id);

        var fila = await FilaAsync(operador, CenarioDeProducao.Corte);

        var posicaoDoSegundo = fila.ToList().FindIndex(e => e.OrdemProducaoId == ordemDoSegundo.Ordem.Id);
        var posicaoDoPrimeiro = fila.ToList().FindIndex(e => e.OrdemProducaoId == ordemDoPrimeiro.Ordem.Id);
        Assert.True(posicaoDoSegundo >= 0 && posicaoDoPrimeiro >= 0);
        Assert.True(posicaoDoSegundo < posicaoDoPrimeiro);
    }

    [Fact]
    public async Task Fila_nao_traz_op_cancelada()
    {
        var cenario = await CenarioDeProducao.CriarAsync(api);
        using var vendedor = await api.ComoAsync("Vendedor");
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var operador = await api.ComoAsync("Operador");
        var pedido = await cenario.PedidoConfirmadoAsync(vendedor, quantidade: 1);
        var ordem = await CenarioDeProducao.OrdemDoPedidoAsync(gerente, pedido.Id);
        (await vendedor.PostAsync($"/api/v1/pedidos/{pedido.Id}/cancelar", content: null)).AssertStatus(HttpStatusCode.OK);

        var fila = await FilaAsync(operador, CenarioDeProducao.Corte);

        Assert.DoesNotContain(fila, e => e.OrdemProducaoId == ordem.Ordem.Id);
    }

    [Fact]
    public async Task Fila_sem_estacao_devolve_400()
    {
        using var operador = await api.ComoAsync("Operador");

        var resposta = await operador.GetAsync("/api/v1/etapas/minhas");

        resposta.AssertStatus(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(99)] // não existe
    [InlineData(6)] // Expedição: existe, mas não gera etapa
    public async Task Fila_de_estacao_inexistente_ou_sem_etapa_devolve_422(byte estacao)
    {
        using var operador = await api.ComoAsync("Operador");

        var resposta = await operador.GetAsync($"/api/v1/etapas/minhas?estacaoId={estacao}");

        resposta.AssertStatus(HttpStatusCode.UnprocessableEntity);
        Assert.Equal("ESTACAO_INEXISTENTE", await resposta.CodigoAsync());
    }
}
