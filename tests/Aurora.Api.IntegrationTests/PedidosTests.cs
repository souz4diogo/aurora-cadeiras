using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Aurora.Application.Clientes;
using Aurora.Application.Comum;
using Aurora.Application.Pedidos;
using Aurora.Application.Produtos;
using Microsoft.Data.SqlClient;

namespace Aurora.Api.IntegrationTests;

[Collection("api")]
public sealed partial class PedidosTests(ApiFactory api)
{
    private static readonly DateOnly Prazo = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);

    [GeneratedRegex(@"^PV-\d{4}-\d{5}$")]
    private static partial Regex FormatoDoNumero();

    private static async Task<ClienteDto> NovoClienteAsync(HttpClient admin, bool ativo = true)
    {
        var resposta = await admin.PostAsJsonAsync(
            "/api/v1/clientes", new ClienteRequest("Cliente de Pedido", DadosDeTeste.Digitos(11), null, null, null, null));
        resposta.AssertStatus(HttpStatusCode.Created);
        var cliente = (await resposta.Content.ReadFromJsonAsync<ClienteDto>())!;
        if (!ativo)
        {
            (await admin.PatchAsJsonAsync($"/api/v1/clientes/{cliente.Id}/ativo", new AlterarAtivoRequest(false)))
                .AssertStatus(HttpStatusCode.OK);
        }

        return cliente;
    }

    private static async Task<List<ProdutoDto>> ProdutosDoSeedAsync(HttpClient admin) =>
        (await admin.GetFromJsonAsync<Pagina<ProdutoDto>>("/api/v1/produtos?pageSize=100"))!
            .Items.Where(p => p.Codigo is "CE-100" or "CJ-100" or "CS-100").OrderBy(p => p.Codigo, StringComparer.Ordinal).ToList();

    private static PedidoRequest Request(int clienteId, params PedidoItemRequest[] itens) =>
        new(clienteId, Prazo, "Entrega parcelada", itens);

    private static async Task<PedidoDto> CriarAsync(HttpClient vendedor, PedidoRequest request)
    {
        var resposta = await vendedor.PostAsJsonAsync("/api/v1/pedidos", request);
        resposta.AssertStatus(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<PedidoDto>())!;
    }

    private async Task<HttpClient> NovoVendedorAsync()
    {
        var (usuario, senha) = await api.CriarUsuarioAsync("Vendedor");
        var token = await api.LoginAsync(usuario.Email, senha);
        var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return cliente;
    }

    private async Task<T> ConsultarAsync<T>(string sql, int pedidoId)
    {
        await using var conexao = new SqlConnection(api.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        comando.Parameters.AddWithValue("@id", pedidoId);
        return (T)(await comando.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Criar_devolve_201_com_numero_do_banco_total_calculado_e_vendedor_logado()
    {
        using var admin = await api.ComoAdministradorAsync();
        using var vendedor = await api.ComoAsync("Vendedor");
        var cliente = await NovoClienteAsync(admin);
        var produtos = await ProdutosDoSeedAsync(admin);

        var resposta = await vendedor.PostAsJsonAsync(
            "/api/v1/pedidos",
            Request(cliente.Id, new PedidoItemRequest(produtos[0].Id, 4), new PedidoItemRequest(produtos[1].Id, 2)));

        resposta.AssertStatus(HttpStatusCode.Created);
        var pedido = (await resposta.Content.ReadFromJsonAsync<PedidoDto>())!;
        Assert.Matches(FormatoDoNumero(), pedido.Numero);
        Assert.Equal("Aberto", pedido.Status);
        Assert.Equal(4 * produtos[0].PrecoVenda + 2 * produtos[1].PrecoVenda, pedido.ValorTotal);
        Assert.Equal(cliente.Nome, pedido.ClienteNome);
        Assert.Equal(Prazo, pedido.PrazoEntrega);
        Assert.Equal("Entrega parcelada", pedido.Observacao);
        Assert.False(pedido.Atrasado);
        Assert.Equal(2, pedido.Itens.Count);
        Assert.All(pedido.Itens, i => Assert.Equal(i.Quantidade * i.PrecoUnitario, i.Subtotal));
        Assert.EndsWith($"/api/v1/pedidos/{pedido.Id}", resposta.Headers.Location?.ToString(), StringComparison.Ordinal);
        Assert.True(pedido.DataPedido > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task Criar_grava_itens_valor_total_e_auditoria_no_banco()
    {
        using var admin = await api.ComoAdministradorAsync();
        var cliente = await NovoClienteAsync(admin);
        var produtos = await ProdutosDoSeedAsync(admin);
        var pedido = await CriarAsync(admin, Request(cliente.Id, new PedidoItemRequest(produtos[0].Id, 3)));

        var itens = await ConsultarAsync<int>("SELECT COUNT(*) FROM dbo.PedidoItem WHERE PedidoId = @id", pedido.Id);
        var total = await ConsultarAsync<decimal>("SELECT ValorTotal FROM dbo.Pedido WHERE PedidoId = @id", pedido.Id);
        var status = await ConsultarAsync<string>("SELECT Status FROM dbo.Pedido WHERE PedidoId = @id", pedido.Id);
        var auditorias = await ConsultarAsync<int>(
            "SELECT COUNT(*) FROM dbo.Auditoria WHERE Tabela = N'Pedido' AND Registro = CAST(@id AS varchar(50)) AND Acao = 'I'", pedido.Id);

        Assert.Equal(1, itens);
        Assert.Equal(3 * produtos[0].PrecoVenda, total);
        Assert.Equal("Aberto", status);
        Assert.Equal(1, auditorias);
    }

    [Fact]
    public async Task Numeros_dos_pedidos_sao_unicos_e_crescentes()
    {
        using var admin = await api.ComoAdministradorAsync();
        var cliente = await NovoClienteAsync(admin);
        var produto = (await ProdutosDoSeedAsync(admin))[0];
        var pedidoA = await CriarAsync(admin, Request(cliente.Id, new PedidoItemRequest(produto.Id, 1)));
        var pedidoB = await CriarAsync(admin, Request(cliente.Id, new PedidoItemRequest(produto.Id, 1)));

        Assert.NotEqual(pedidoA.Numero, pedidoB.Numero);
        Assert.True(string.CompareOrdinal(pedidoB.Numero, pedidoA.Numero) > 0);
    }

    [Fact]
    public async Task Entrada_invalida_devolve_400()
    {
        using var admin = await api.ComoAdministradorAsync();
        var cliente = await NovoClienteAsync(admin);
        var produto = (await ProdutosDoSeedAsync(admin))[0];
        var item = new PedidoItemRequest(produto.Id, 1);
        var ontem = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-5);

        var invalidos = new[]
        {
            Request(cliente.Id),
            Request(cliente.Id, new PedidoItemRequest(produto.Id, 0)),
            Request(cliente.Id, item, item),
            new PedidoRequest(cliente.Id, ontem, null, [item]),
            new PedidoRequest(cliente.Id, Prazo, new string('x', 301), [item]),
            Request(0, item),
        };

        foreach (var request in invalidos)
        {
            var resposta = await admin.PostAsJsonAsync("/api/v1/pedidos", request);
            resposta.AssertStatus(HttpStatusCode.BadRequest);
            Assert.Equal("VALIDACAO", await resposta.CodigoAsync());
        }
    }

    [Fact]
    public async Task Cliente_inexistente_ou_inativo_e_produto_inexistente_ou_inativo_devolvem_422()
    {
        using var admin = await api.ComoAdministradorAsync();
        var ativo = await NovoClienteAsync(admin);
        var inativo = await NovoClienteAsync(admin, ativo: false);
        var produto = (await ProdutosDoSeedAsync(admin))[0];
        var codigo = ("I" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant();
        var criado = (await (await admin.PostAsJsonAsync("/api/v1/produtos", new ProdutoRequest(codigo, "Produto Inativo", 1, 10m)))
            .Content.ReadFromJsonAsync<ProdutoDto>())!;
        await admin.PatchAsJsonAsync($"/api/v1/produtos/{criado.Id}/ativo", new AlterarAtivoRequest(false));
        var item = new PedidoItemRequest(produto.Id, 1);

        var casos = new (PedidoRequest Request, string Codigo)[]
        {
            (Request(987654, item), "CLIENTE_INEXISTENTE"),
            (Request(inativo.Id, item), "CLIENTE_INATIVO"),
            (Request(ativo.Id, new PedidoItemRequest(987654, 1)), "PRODUTO_INEXISTENTE"),
            (Request(ativo.Id, new PedidoItemRequest(criado.Id, 1)), "PRODUTO_INATIVO"),
        };

        foreach (var (request, esperado) in casos)
        {
            var resposta = await admin.PostAsJsonAsync("/api/v1/pedidos", request);
            resposta.AssertStatus(HttpStatusCode.UnprocessableEntity);
            Assert.Equal(esperado, await resposta.CodigoAsync());
        }
    }

    [Fact]
    public async Task Obter_inexistente_devolve_404()
    {
        using var admin = await api.ComoAdministradorAsync();

        var resposta = await admin.GetAsync("/api/v1/pedidos/987654");

        resposta.AssertStatus(HttpStatusCode.NotFound);
        Assert.Equal("PEDIDO_NAO_ENCONTRADO", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Atualizar_troca_itens_recalcula_o_total_e_mantem_o_preco_do_item_antigo()
    {
        using var admin = await api.ComoAdministradorAsync();
        var cliente = await NovoClienteAsync(admin);
        var produtos = await ProdutosDoSeedAsync(admin);
        var original = await CriarAsync(admin, Request(cliente.Id, new PedidoItemRequest(produtos[0].Id, 2), new PedidoItemRequest(produtos[1].Id, 1)));
        var precoAntigo = produtos[0].PrecoVenda;
        (await admin.PutAsJsonAsync(
            $"/api/v1/produtos/{produtos[0].Id}", new ProdutoRequest(produtos[0].Codigo, produtos[0].Nome, produtos[0].LinhaProdutoId, precoAntigo + 500m)))
            .AssertStatus(HttpStatusCode.OK);

        var resposta = await admin.PutAsJsonAsync(
            $"/api/v1/pedidos/{original.Id}",
            new PedidoRequest(cliente.Id, Prazo.AddDays(5), "Revisado", [new PedidoItemRequest(produtos[0].Id, 5), new PedidoItemRequest(produtos[2].Id, 1)]));

        resposta.AssertStatus(HttpStatusCode.OK);
        var atualizado = (await resposta.Content.ReadFromJsonAsync<PedidoDto>())!;
        Assert.Equal(original.Numero, atualizado.Numero);
        Assert.Equal(Prazo.AddDays(5), atualizado.PrazoEntrega);
        Assert.Equal("Revisado", atualizado.Observacao);
        Assert.Equal([produtos[0].Codigo, produtos[2].Codigo], atualizado.Itens.Select(i => i.ProdutoCodigo));
        Assert.Equal(precoAntigo, atualizado.Itens[0].PrecoUnitario); // item antigo mantém o preço
        Assert.Equal(produtos[2].PrecoVenda, atualizado.Itens[1].PrecoUnitario); // item novo, preço atual
        Assert.Equal((5 * precoAntigo) + produtos[2].PrecoVenda, atualizado.ValorTotal);

        var persistido = await admin.GetFromJsonAsync<PedidoDto>($"/api/v1/pedidos/{original.Id}");
        Assert.Equal(atualizado.ValorTotal, persistido!.ValorTotal);
        Assert.Equal(2, persistido.Itens.Count);
        Assert.Equal(2, await ConsultarAsync<int>("SELECT COUNT(*) FROM dbo.PedidoItem WHERE PedidoId = @id", original.Id));
    }

    [Fact]
    public async Task Cancelar_usa_a_procedure_e_impede_nova_edicao_e_novo_cancelamento()
    {
        using var vendedor = await api.ComoAsync("Vendedor");
        using var admin = await api.ComoAdministradorAsync();
        var cliente = await NovoClienteAsync(admin);
        var produto = (await ProdutosDoSeedAsync(admin))[0];
        var pedido = await CriarAsync(vendedor, Request(cliente.Id, new PedidoItemRequest(produto.Id, 1)));

        var cancelado = await vendedor.PostAsync($"/api/v1/pedidos/{pedido.Id}/cancelar", null);
        var edicao = await vendedor.PutAsJsonAsync($"/api/v1/pedidos/{pedido.Id}", Request(cliente.Id, new PedidoItemRequest(produto.Id, 2)));
        var outraVez = await vendedor.PostAsync($"/api/v1/pedidos/{pedido.Id}/cancelar", null);

        cancelado.AssertStatus(HttpStatusCode.OK);
        Assert.Equal("Cancelado", (await cancelado.Content.ReadFromJsonAsync<PedidoDto>())!.Status);
        Assert.Equal("Cancelado", await ConsultarAsync<string>("SELECT Status FROM dbo.Pedido WHERE PedidoId = @id", pedido.Id));
        edicao.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("STATUS_INVALIDO", await edicao.CodigoAsync());
        outraVez.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("STATUS_INVALIDO", await outraVez.CodigoAsync());
    }

    [Fact]
    public async Task Cancelar_inexistente_devolve_404()
    {
        using var admin = await api.ComoAdministradorAsync();

        var resposta = await admin.PostAsync("/api/v1/pedidos/987654/cancelar", null);

        resposta.AssertStatus(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Vendedor_so_enxerga_e_altera_os_proprios_pedidos_mas_gerente_e_admin_veem_todos()
    {
        using var admin = await api.ComoAdministradorAsync();
        using var gerente = await api.ComoAsync("GerenteProducao");
        using var almoxarife = await api.ComoAsync("Almoxarife");
        using var dono = await NovoVendedorAsync();
        using var outro = await NovoVendedorAsync();
        var cliente = await NovoClienteAsync(admin);
        var produto = (await ProdutosDoSeedAsync(admin))[0];
        var pedido = await CriarAsync(dono, Request(cliente.Id, new PedidoItemRequest(produto.Id, 1)));

        var doOutro = await outro.GetAsync($"/api/v1/pedidos/{pedido.Id}");
        var edicaoDoOutro = await outro.PutAsJsonAsync($"/api/v1/pedidos/{pedido.Id}", Request(cliente.Id, new PedidoItemRequest(produto.Id, 9)));
        var cancelamentoDoOutro = await outro.PostAsync($"/api/v1/pedidos/{pedido.Id}/cancelar", null);
        var listaDoOutro = await outro.GetFromJsonAsync<Pagina<PedidoResumoDto>>($"/api/v1/pedidos?clienteId={cliente.Id}");
        var listaDoDono = await dono.GetFromJsonAsync<Pagina<PedidoResumoDto>>($"/api/v1/pedidos?clienteId={cliente.Id}");

        doOutro.AssertStatus(HttpStatusCode.NotFound);
        edicaoDoOutro.AssertStatus(HttpStatusCode.NotFound);
        cancelamentoDoOutro.AssertStatus(HttpStatusCode.NotFound);
        Assert.Empty(listaDoOutro!.Items);
        Assert.Single(listaDoDono!.Items);
        foreach (var leitor in new[] { admin, gerente, almoxarife })
        {
            (await leitor.GetAsync($"/api/v1/pedidos/{pedido.Id}")).AssertStatus(HttpStatusCode.OK);
        }

        // A tentativa do outro vendedor não alterou nada
        var intacto = await admin.GetFromJsonAsync<PedidoDto>($"/api/v1/pedidos/{pedido.Id}");
        Assert.Equal("Aberto", intacto!.Status);
        Assert.Equal(1, intacto.Itens.Single().Quantidade);
    }

    [Fact]
    public async Task Listar_filtra_por_status_cliente_periodo_e_atraso_e_ordena_do_mais_recente()
    {
        using var admin = await api.ComoAdministradorAsync();
        var cliente = await NovoClienteAsync(admin);
        var produto = (await ProdutosDoSeedAsync(admin))[0];
        var primeiro = await CriarAsync(admin, Request(cliente.Id, new PedidoItemRequest(produto.Id, 1)));
        var segundo = await CriarAsync(admin, Request(cliente.Id, new PedidoItemRequest(produto.Id, 2)));
        var terceiro = await CriarAsync(admin, Request(cliente.Id, new PedidoItemRequest(produto.Id, 3)));
        (await admin.PostAsync($"/api/v1/pedidos/{terceiro.Id}/cancelar", null)).AssertStatus(HttpStatusCode.OK);

        // Simula um pedido confirmado e vencido (a confirmação em si é da fase 3)
        await using (var conexao = new SqlConnection(api.ConnectionString))
        {
            await conexao.OpenAsync();
            await using var comando = conexao.CreateCommand();
            comando.CommandText = "UPDATE dbo.Pedido SET Status = 'Confirmado', PrazoEntrega = DATEADD(DAY, -3, CAST(SYSUTCDATETIME() AS date)) WHERE PedidoId = @id";
            comando.Parameters.AddWithValue("@id", segundo.Id);
            await comando.ExecuteNonQueryAsync();
        }

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var todos = await admin.GetFromJsonAsync<Pagina<PedidoResumoDto>>($"/api/v1/pedidos?clienteId={cliente.Id}");
        var abertos = await admin.GetFromJsonAsync<Pagina<PedidoResumoDto>>($"/api/v1/pedidos?clienteId={cliente.Id}&status=Aberto");
        var atrasados = await admin.GetFromJsonAsync<Pagina<PedidoResumoDto>>($"/api/v1/pedidos?clienteId={cliente.Id}&atrasados=true");
        var emDia = await admin.GetFromJsonAsync<Pagina<PedidoResumoDto>>($"/api/v1/pedidos?clienteId={cliente.Id}&atrasados=false");
        var hojeNoPeriodo = await admin.GetFromJsonAsync<Pagina<PedidoResumoDto>>($"/api/v1/pedidos?clienteId={cliente.Id}&de={hoje:yyyy-MM-dd}&ate={hoje:yyyy-MM-dd}");
        var amanha = await admin.GetFromJsonAsync<Pagina<PedidoResumoDto>>($"/api/v1/pedidos?clienteId={cliente.Id}&de={hoje.AddDays(1):yyyy-MM-dd}");

        Assert.Equal([terceiro.Id, segundo.Id, primeiro.Id], todos!.Items.Select(i => i.Id));
        Assert.Equal([primeiro.Id], abertos!.Items.Select(i => i.Id));
        Assert.Equal([segundo.Id], atrasados!.Items.Select(i => i.Id));
        Assert.True(atrasados.Items[0].Atrasado);
        Assert.Equal("Confirmado", atrasados.Items[0].Status);
        Assert.Equal([terceiro.Id, primeiro.Id], emDia!.Items.Select(i => i.Id));
        Assert.Equal(3, hojeNoPeriodo!.Total);
        Assert.Empty(amanha!.Items);
        Assert.All(todos.Items, i => Assert.Equal(cliente.Nome, i.ClienteNome));
        Assert.Equal([1, 1, 1], todos.Items.Select(i => i.QuantidadeItens));
    }

    [Fact]
    public async Task Listar_pagina_e_limita_o_tamanho()
    {
        using var admin = await api.ComoAdministradorAsync();

        var pagina = await admin.GetFromJsonAsync<Pagina<PedidoResumoDto>>("/api/v1/pedidos?page=1&pageSize=5000");

        Assert.Equal(Paginacao.TamanhoMaximo, pagina!.PageSize);
    }

    [Fact]
    public async Task Status_invalido_na_consulta_devolve_400()
    {
        using var admin = await api.ComoAdministradorAsync();

        var resposta = await admin.GetAsync("/api/v1/pedidos?status=Inexistente");

        resposta.AssertStatus(HttpStatusCode.BadRequest);
    }
}
