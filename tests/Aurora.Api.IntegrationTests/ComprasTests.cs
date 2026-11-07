using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Aurora.Application.Clientes;
using Aurora.Application.Comum;
using Aurora.Application.Compras;
using Aurora.Application.Estoque;
using Aurora.Application.Fornecedores;
using Aurora.Application.MateriasPrimas;

namespace Aurora.Api.IntegrationTests;

/// <summary>Pedidos de compra (RF16) e recebimento com entrada no estoque (RF17).</summary>
[Collection("api")]
public sealed partial class ComprasTests(ApiFactory api)
{
    [GeneratedRegex(@"^PC-\d{4}-\d{5}$")]
    private static partial Regex FormatoDoNumero();

    private static PedidoCompraItemRequest Item(int materiaPrimaId, decimal quantidade, decimal? custo) =>
        new(materiaPrimaId, quantidade, custo);

    // O mesmo "hoje" da API: atrasos contam pelo dia de Brasília, não pelo de UTC (A81)
    private static DateOnly Hoje => DiaDeNegocio.De(DateTime.UtcNow);

    private static async Task<T> CriarAsync<T>(HttpClient cliente, string rota, object corpo)
    {
        var resposta = await cliente.PostAsJsonAsync(rota, corpo);
        resposta.AssertStatus(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<T>())!;
    }

    private static Task<FornecedorDto> NovoFornecedorAsync(HttpClient admin, bool comunidade = false) =>
        CriarAsync<FornecedorDto>(
            admin, "/api/v1/fornecedores", new FornecedorRequest("Fornecedor " + Guid.NewGuid().ToString("N")[..6], DadosDeTeste.Digitos(14), null, null, comunidade));

    private static Task<MateriaPrimaDto> NovaMateriaAsync(HttpClient admin, decimal minimo = 1m, decimal custo = 10m) =>
        CriarAsync<MateriaPrimaDto>(
            admin,
            "/api/v1/materias-primas",
            new MateriaPrimaRequest(("M" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant(), "Insumo de Compra", "m2", minimo, custo, null));

    private static async Task<PedidoCompraDto> SolicitarAsync(
        HttpClient almoxarife, int fornecedorId, params PedidoCompraItemRequest[] itens)
    {
        var resposta = await almoxarife.PostAsJsonAsync("/api/v1/pedidos-compra", new PedidoCompraRequest(fornecedorId, null, itens));
        resposta.AssertStatus(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<PedidoCompraDto>())!;
    }

    private static async Task<PedidoCompraDto> EmitirAsync(HttpClient almoxarife, int pedidoId)
    {
        var resposta = await almoxarife.PostAsync($"/api/v1/pedidos-compra/{pedidoId}/emitir", content: null);
        resposta.AssertStatus(HttpStatusCode.OK);
        return (await resposta.Content.ReadFromJsonAsync<PedidoCompraDto>())!;
    }

    private static Task<HttpResponseMessage> ReceberAsync(HttpClient almoxarife, int itemId, decimal quantidade) =>
        almoxarife.PostAsJsonAsync($"/api/v1/pedidos-compra/itens/{itemId}/receber", new ReceberItemRequest(quantidade));

    private static async Task<decimal> SaldoAsync(HttpClient almoxarife, int materiaId) =>
        (await almoxarife.GetFromJsonAsync<MateriaPrimaDto>($"/api/v1/materias-primas/{materiaId}"))!.EstoqueAtual;

    [Fact]
    public async Task Solicitar_cria_o_pedido_em_Solicitado_com_numero_comprador_e_total()
    {
        using var admin = await api.ComoAdministradorAsync();
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var fornecedor = await NovoFornecedorAsync(admin);
        var madeira = await NovaMateriaAsync(admin);
        var tecido = await NovaMateriaAsync(admin);

        var resposta = await almoxarife.PostAsJsonAsync(
            "/api/v1/pedidos-compra",
            new PedidoCompraRequest(
                fornecedor.Id, Hoje.AddDays(10), [new(madeira.Id, 5m, 12.5m), new(tecido.Id, 2.5m, 8m)]));

        resposta.AssertStatus(HttpStatusCode.Created);
        Assert.StartsWith("/api/v1/pedidos-compra/", resposta.Headers.Location!.AbsolutePath, StringComparison.Ordinal);
        var pedido = (await resposta.Content.ReadFromJsonAsync<PedidoCompraDto>())!;
        Assert.Matches(FormatoDoNumero(), pedido.Numero);
        Assert.Equal("Solicitado", pedido.Status);
        Assert.Equal(fornecedor.RazaoSocial, pedido.FornecedorNome);
        Assert.False(string.IsNullOrEmpty(pedido.CompradorNome));
        Assert.Equal((5m * 12.5m) + (2.5m * 8m), pedido.ValorTotal);
        Assert.True(pedido.PodeEmitir);
        Assert.True(pedido.PodeCancelar);
        Assert.False(pedido.PodeReceber);
        Assert.All(pedido.Itens, i => Assert.Equal(0m, i.QuantidadeRecebida));
        Assert.Equal(5m, pedido.Itens.Single(i => i.MateriaPrimaId == madeira.Id).Saldo);
    }

    [Fact]
    public async Task Item_sem_custo_usa_o_custo_atual_da_materia_prima_e_marca_a_comunidade_tradicional()
    {
        using var admin = await api.ComoAdministradorAsync();
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var fornecedor = await NovoFornecedorAsync(admin, comunidade: true);
        var materia = await NovaMateriaAsync(admin, custo: 33.3333m);

        var pedido = await SolicitarAsync(almoxarife, fornecedor.Id, Item(materia.Id, 3m, null));

        Assert.Equal(33.3333m, Assert.Single(pedido.Itens).CustoUnitario);
        Assert.True(pedido.ComunidadeTradicional);
    }

    [Fact]
    public async Task Fluxo_completo_solicitado_emitido_parcial_recebido_com_entrada_no_estoque()
    {
        using var admin = await api.ComoAdministradorAsync();
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var fornecedor = await NovoFornecedorAsync(admin);
        var materia = await NovaMateriaAsync(admin, custo: 10m);
        var pedido = await SolicitarAsync(almoxarife, fornecedor.Id, Item(materia.Id, 10m, 14.2m));
        var item = Assert.Single(pedido.Itens);
        Assert.Equal(0m, await SaldoAsync(almoxarife, materia.Id));

        var emitido = await EmitirAsync(almoxarife, pedido.Id);
        var parcial = await ReceberAsync(almoxarife, item.Id, 4.125m);

        Assert.Equal("Emitido", emitido.Status);
        Assert.True(emitido.PodeReceber);
        Assert.False(emitido.PodeEmitir);
        parcial.AssertStatus(HttpStatusCode.OK);
        var depoisDoParcial = (await parcial.Content.ReadFromJsonAsync<PedidoCompraDto>())!;
        Assert.Equal("ParcialmenteRecebido", depoisDoParcial.Status);
        Assert.Equal(4.125m, depoisDoParcial.Itens[0].QuantidadeRecebida); // as 3 casas decimais chegam inteiras
        Assert.Equal(5.875m, depoisDoParcial.Itens[0].Saldo);
        Assert.False(depoisDoParcial.PodeCancelar);
        Assert.Equal(4.125m, await SaldoAsync(almoxarife, materia.Id));
        var cadastro = await almoxarife.GetFromJsonAsync<MateriaPrimaDto>($"/api/v1/materias-primas/{materia.Id}");
        Assert.Equal(14.2m, cadastro!.CustoUnitario); // o custo do pedido vira o custo atual do material

        var resto = await ReceberAsync(almoxarife, item.Id, 5.875m);

        var completo = (await resto.Content.ReadFromJsonAsync<PedidoCompraDto>())!;
        Assert.Equal("Recebido", completo.Status);
        Assert.False(completo.PodeReceber);
        Assert.Equal(10m, await SaldoAsync(almoxarife, materia.Id));
    }

    [Fact]
    public async Task Pedido_com_dois_itens_so_vira_Recebido_quando_o_ultimo_completa()
    {
        using var admin = await api.ComoAdministradorAsync();
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var fornecedor = await NovoFornecedorAsync(admin);
        var a = await NovaMateriaAsync(admin);
        var b = await NovaMateriaAsync(admin);
        var pedido = await SolicitarAsync(almoxarife, fornecedor.Id, Item(a.Id, 2m, 1m), Item(b.Id, 3m, 1m));
        await EmitirAsync(almoxarife, pedido.Id);
        var itemA = pedido.Itens.Single(i => i.MateriaPrimaId == a.Id);
        var itemB = pedido.Itens.Single(i => i.MateriaPrimaId == b.Id);

        var primeiro = await (await ReceberAsync(almoxarife, itemA.Id, 2m)).Content.ReadFromJsonAsync<PedidoCompraDto>();
        var ultimo = await (await ReceberAsync(almoxarife, itemB.Id, 3m)).Content.ReadFromJsonAsync<PedidoCompraDto>();

        Assert.Equal("ParcialmenteRecebido", primeiro!.Status);
        Assert.Equal("Recebido", ultimo!.Status);
    }

    [Fact]
    public async Task Receber_acima_do_pedido_devolve_422_e_nao_mexe_no_estoque()
    {
        using var admin = await api.ComoAdministradorAsync();
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var fornecedor = await NovoFornecedorAsync(admin);
        var materia = await NovaMateriaAsync(admin);
        var pedido = await SolicitarAsync(almoxarife, fornecedor.Id, Item(materia.Id, 5m, 1m));
        await EmitirAsync(almoxarife, pedido.Id);
        var item = pedido.Itens[0];
        (await ReceberAsync(almoxarife, item.Id, 3m)).AssertStatus(HttpStatusCode.OK);

        var excedente = await ReceberAsync(almoxarife, item.Id, 2.001m);

        excedente.AssertStatus(HttpStatusCode.UnprocessableEntity);
        Assert.Equal("RECEBIMENTO_EXCEDENTE", await excedente.CodigoAsync());
        Assert.Equal(3m, await SaldoAsync(almoxarife, materia.Id));
    }

    [Fact]
    public async Task Receber_pedido_ainda_nao_emitido_ou_cancelado_devolve_409()
    {
        using var admin = await api.ComoAdministradorAsync();
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var fornecedor = await NovoFornecedorAsync(admin);
        var materia = await NovaMateriaAsync(admin);
        var solicitado = await SolicitarAsync(almoxarife, fornecedor.Id, Item(materia.Id, 5m, 1m));
        var cancelado = await SolicitarAsync(almoxarife, fornecedor.Id, Item(materia.Id, 5m, 1m));
        (await almoxarife.PostAsync($"/api/v1/pedidos-compra/{cancelado.Id}/cancelar", content: null)).AssertStatus(HttpStatusCode.OK);

        var doSolicitado = await ReceberAsync(almoxarife, solicitado.Itens[0].Id, 1m);
        var doCancelado = await ReceberAsync(almoxarife, cancelado.Itens[0].Id, 1m);

        doSolicitado.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("STATUS_INVALIDO", await doSolicitado.CodigoAsync());
        doCancelado.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal(0m, await SaldoAsync(almoxarife, materia.Id));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Receber_quantidade_nao_positiva_devolve_400(int quantidade)
    {
        using var almoxarife = await api.ComoAsync("Almoxarife");

        var resposta = await ReceberAsync(almoxarife, 2147000000, quantidade);

        resposta.AssertStatus(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Receber_item_inexistente_devolve_404()
    {
        using var almoxarife = await api.ComoAsync("Almoxarife");

        var resposta = await ReceberAsync(almoxarife, 2147000000, 1m);

        resposta.AssertStatus(HttpStatusCode.NotFound);
        Assert.Equal("ITEM_DE_COMPRA_NAO_ENCONTRADO", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Emitir_so_vale_uma_vez_e_cancelar_so_antes_de_receber()
    {
        using var admin = await api.ComoAdministradorAsync();
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var fornecedor = await NovoFornecedorAsync(admin);
        var materia = await NovaMateriaAsync(admin);
        var pedido = await SolicitarAsync(almoxarife, fornecedor.Id, Item(materia.Id, 5m, 1m));
        await EmitirAsync(almoxarife, pedido.Id);

        var reemitir = await almoxarife.PostAsync($"/api/v1/pedidos-compra/{pedido.Id}/emitir", content: null);
        (await ReceberAsync(almoxarife, pedido.Itens[0].Id, 1m)).AssertStatus(HttpStatusCode.OK);
        var cancelarDepois = await almoxarife.PostAsync($"/api/v1/pedidos-compra/{pedido.Id}/cancelar", content: null);

        reemitir.AssertStatus(HttpStatusCode.Conflict);
        cancelarDepois.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("STATUS_INVALIDO", await cancelarDepois.CodigoAsync());
        var atual = await almoxarife.GetFromJsonAsync<PedidoCompraDto>($"/api/v1/pedidos-compra/{pedido.Id}");
        Assert.Equal("ParcialmenteRecebido", atual!.Status); // o recebimento não foi desfeito
    }

    [Fact]
    public async Task Cancelar_pedido_emitido_sem_recebimento_funciona_e_cancelado_nao_volta()
    {
        using var admin = await api.ComoAdministradorAsync();
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var fornecedor = await NovoFornecedorAsync(admin);
        var materia = await NovaMateriaAsync(admin);
        var pedido = await SolicitarAsync(almoxarife, fornecedor.Id, Item(materia.Id, 5m, 1m));
        await EmitirAsync(almoxarife, pedido.Id);

        var cancelado = await almoxarife.PostAsync($"/api/v1/pedidos-compra/{pedido.Id}/cancelar", content: null);
        var emitirDeNovo = await almoxarife.PostAsync($"/api/v1/pedidos-compra/{pedido.Id}/emitir", content: null);
        var cancelarDeNovo = await almoxarife.PostAsync($"/api/v1/pedidos-compra/{pedido.Id}/cancelar", content: null);

        cancelado.AssertStatus(HttpStatusCode.OK);
        Assert.Equal("Cancelado", (await cancelado.Content.ReadFromJsonAsync<PedidoCompraDto>())!.Status);
        emitirDeNovo.AssertStatus(HttpStatusCode.Conflict);
        cancelarDeNovo.AssertStatus(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Cancelar_e_receber_ao_mesmo_tempo_nunca_deixam_o_pedido_cancelado_com_material_no_estoque()
    {
        using var admin = await api.ComoAdministradorAsync();
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var fornecedor = await NovoFornecedorAsync(admin);

        for (var rodada = 0; rodada < 6; rodada++)
        {
            var materia = await NovaMateriaAsync(admin);
            var pedido = await SolicitarAsync(almoxarife, fornecedor.Id, Item(materia.Id, 5m, 1m));
            await EmitirAsync(almoxarife, pedido.Id);

            var cancelar = almoxarife.PostAsync($"/api/v1/pedidos-compra/{pedido.Id}/cancelar", content: null);
            var receber = ReceberAsync(almoxarife, pedido.Itens[0].Id, 5m);
            var respostas = await Task.WhenAll(cancelar, receber);

            var final = await almoxarife.GetFromJsonAsync<PedidoCompraDto>($"/api/v1/pedidos-compra/{pedido.Id}");
            var saldo = await SaldoAsync(almoxarife, materia.Id);
            var cancelou = respostas[0].StatusCode == HttpStatusCode.OK;
            var recebeu = respostas[1].StatusCode == HttpStatusCode.OK;
            Assert.NotEqual(cancelou, recebeu); // exatamente um dos dois vence
            if (cancelou)
            {
                Assert.Equal("Cancelado", final!.Status);
                Assert.Equal(0m, saldo);
            }
            else
            {
                Assert.Equal("Recebido", final!.Status);
                Assert.Equal(5m, saldo);
            }
        }
    }

    [Fact]
    public async Task Estoque_critico_gera_alerta_a_compra_recebida_repoe_o_saldo()
    {
        using var admin = await api.ComoAdministradorAsync();
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var fornecedor = await NovoFornecedorAsync(admin);
        var materia = await NovaMateriaAsync(admin, minimo: 10m);
        (await almoxarife.PostAsJsonAsync("/api/v1/estoque/ajustes", new AjusteDeEstoqueRequest(materia.Id, 4m, "Contagem inicial")))
            .AssertStatus(HttpStatusCode.OK);
        var criticos = await almoxarife.GetFromJsonAsync<Pagina<SaldoDeMateriaPrimaDto>>($"/api/v1/estoque/materias-primas?critico=true&busca={materia.Codigo}");
        Assert.True(Assert.Single(criticos!.Items).Critico);

        var pedido = await SolicitarAsync(almoxarife, fornecedor.Id, Item(materia.Id, 30m, 9m));
        await EmitirAsync(almoxarife, pedido.Id);
        (await ReceberAsync(almoxarife, pedido.Itens[0].Id, 30m)).AssertStatus(HttpStatusCode.OK);

        Assert.Equal(34m, await SaldoAsync(almoxarife, materia.Id));
        var depois = await almoxarife.GetFromJsonAsync<Pagina<SaldoDeMateriaPrimaDto>>($"/api/v1/estoque/materias-primas?busca={materia.Codigo}");
        Assert.False(Assert.Single(depois!.Items).Critico);
        var movimentos = await almoxarife.GetFromJsonAsync<Pagina<MovimentoEstoqueDto>>($"/api/v1/estoque/movimentos?materiaPrimaId={materia.Id}");
        Assert.Contains(movimentos!.Items, m => m.Origem == "Compra" && m.Quantidade == 30m);
    }

    [Theory]
    [InlineData("itens vazios")]
    [InlineData("matéria repetida")]
    [InlineData("quantidade zero")]
    [InlineData("quantidade com 4 casas")]
    [InlineData("custo negativo")]
    [InlineData("data no passado")]
    [InlineData("fornecedor zero")]
    public async Task Solicitacao_invalida_devolve_400(string caso)
    {
        using var admin = await api.ComoAdministradorAsync();
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var fornecedor = await NovoFornecedorAsync(admin);
        var materia = await NovaMateriaAsync(admin);
        var pedido = caso switch
        {
            "itens vazios" => new PedidoCompraRequest(fornecedor.Id, null, []),
            "matéria repetida" => new PedidoCompraRequest(fornecedor.Id, null, [new(materia.Id, 1m, 1m), new(materia.Id, 2m, 1m)]),
            "quantidade zero" => new PedidoCompraRequest(fornecedor.Id, null, [new(materia.Id, 0m, 1m)]),
            "quantidade com 4 casas" => new PedidoCompraRequest(fornecedor.Id, null, [new(materia.Id, 1.0001m, 1m)]),
            "custo negativo" => new PedidoCompraRequest(fornecedor.Id, null, [new(materia.Id, 1m, -1m)]),
            "data no passado" => new PedidoCompraRequest(fornecedor.Id, Hoje.AddDays(-5), [new(materia.Id, 1m, 1m)]),
            _ => new PedidoCompraRequest(0, null, [new(materia.Id, 1m, 1m)]),
        };

        var resposta = await almoxarife.PostAsJsonAsync("/api/v1/pedidos-compra", pedido);

        resposta.AssertStatus(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Fornecedor_ou_materia_inexistente_ou_inativa_devolve_422()
    {
        using var admin = await api.ComoAdministradorAsync();
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var fornecedor = await NovoFornecedorAsync(admin);
        var materia = await NovaMateriaAsync(admin);
        var fornecedorInativo = await NovoFornecedorAsync(admin);
        (await admin.PatchAsJsonAsync($"/api/v1/fornecedores/{fornecedorInativo.Id}/ativo", new AlterarAtivoRequest(false))).AssertStatus(HttpStatusCode.OK);
        var materiaInativa = await NovaMateriaAsync(admin);
        (await admin.PatchAsJsonAsync($"/api/v1/materias-primas/{materiaInativa.Id}/ativo", new AlterarAtivoRequest(false))).AssertStatus(HttpStatusCode.OK);

        var casos = new (PedidoCompraRequest Pedido, string Codigo)[]
        {
            (new(2147000000, null, [new(materia.Id, 1m, 1m)]), "FORNECEDOR_INEXISTENTE"),
            (new(fornecedorInativo.Id, null, [new(materia.Id, 1m, 1m)]), "FORNECEDOR_INATIVO"),
            (new(fornecedor.Id, null, [new(2147000000, 1m, 1m)]), "MATERIA_PRIMA_INEXISTENTE"),
            (new(fornecedor.Id, null, [new(materiaInativa.Id, 1m, 1m)]), "MATERIA_PRIMA_INATIVA"),
        };

        foreach (var (pedido, codigo) in casos)
        {
            var resposta = await almoxarife.PostAsJsonAsync("/api/v1/pedidos-compra", pedido);
            resposta.AssertStatus(HttpStatusCode.UnprocessableEntity);
            Assert.Equal(codigo, await resposta.CodigoAsync());
        }
    }

    [Fact]
    public async Task Listagem_filtra_por_status_fornecedor_e_atraso_e_pagina()
    {
        using var admin = await api.ComoAdministradorAsync();
        using var almoxarife = await api.ComoAsync("Almoxarife");
        using var gerente = await api.ComoAsync("GerenteProducao");
        var fornecedor = await NovoFornecedorAsync(admin);
        var materia = await NovaMateriaAsync(admin);
        var solicitado = await SolicitarAsync(almoxarife, fornecedor.Id, Item(materia.Id, 1m, 1m));
        var emitido = await SolicitarAsync(almoxarife, fornecedor.Id, Item(materia.Id, 1m, 1m));
        await EmitirAsync(almoxarife, emitido.Id);
        var atrasadoResposta = await almoxarife.PostAsJsonAsync(
            "/api/v1/pedidos-compra", new PedidoCompraRequest(fornecedor.Id, Hoje.AddDays(-1), [new(materia.Id, 1m, 1m)]));
        var atrasado = (await atrasadoResposta.Content.ReadFromJsonAsync<PedidoCompraDto>())!;
        await EmitirAsync(almoxarife, atrasado.Id);
        var rota = $"/api/v1/pedidos-compra?fornecedorId={fornecedor.Id}";

        var todos = await gerente.GetFromJsonAsync<Pagina<PedidoCompraResumoDto>>(rota);
        var soSolicitados = await gerente.GetFromJsonAsync<Pagina<PedidoCompraResumoDto>>(rota + "&status=Solicitado");
        var soAtrasados = await gerente.GetFromJsonAsync<Pagina<PedidoCompraResumoDto>>(rota + "&atrasados=true");
        var paginaUm = await gerente.GetFromJsonAsync<Pagina<PedidoCompraResumoDto>>(rota + "&page=1&pageSize=2");

        Assert.Equal([atrasado.Id, emitido.Id, solicitado.Id], todos!.Items.Select(p => p.Id)); // o mais recente primeiro
        Assert.Equal(solicitado.Id, Assert.Single(soSolicitados!.Items).Id);
        var delesAtrasado = Assert.Single(soAtrasados!.Items);
        Assert.Equal(atrasado.Id, delesAtrasado.Id);
        Assert.True(delesAtrasado.Atrasado);
        Assert.Equal(2, paginaUm!.Items.Count);
        Assert.Equal(3, paginaUm.Total);
    }

    [Fact]
    public async Task Detalhe_de_pedido_inexistente_devolve_404()
    {
        using var almoxarife = await api.ComoAsync("Almoxarife");

        var resposta = await almoxarife.GetAsync("/api/v1/pedidos-compra/2147000000");

        resposta.AssertStatus(HttpStatusCode.NotFound);
        Assert.Equal("PEDIDO_COMPRA_NAO_ENCONTRADO", await resposta.CodigoAsync());
    }
}
