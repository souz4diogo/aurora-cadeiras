using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Clientes;
using Aurora.Application.Pedidos;
using Aurora.Application.Produtos;

namespace Aurora.Api.IntegrationTests;

/// <summary>Matriz de permissões (docs/06 §3) dos pedidos de venda: 200/201, 401 e 403 por perfil.</summary>
[Collection("api")]
public class PermissoesPedidosTests(ApiFactory api)
{
    private static readonly string[] Todos =
        ["Administrador", "GerenteProducao", "Operador", "Vendedor", "Almoxarife", "Inspetor", "Cliente"];

    public static TheoryData<string, bool> Leitura => Matriz(["Administrador", "GerenteProducao", "Vendedor", "Almoxarife"]);

    public static TheoryData<string, bool> Escrita => Matriz(["Administrador", "Vendedor"]);

    private static TheoryData<string, bool> Matriz(string[] permitidos)
    {
        var dados = new TheoryData<string, bool>();
        foreach (var perfil in Todos)
        {
            dados.Add(perfil, permitidos.Contains(perfil));
        }

        return dados;
    }

    private static async Task<PedidoRequest> PedidoValidoAsync(HttpClient admin)
    {
        var cliente = (await (await admin.PostAsJsonAsync(
                "/api/v1/clientes", new ClienteRequest("Cliente", DadosDeTeste.Digitos(11), null, null, null, null)))
            .Content.ReadFromJsonAsync<ClienteDto>())!;
        var produtos = (await admin.GetFromJsonAsync<Aurora.Application.Comum.Pagina<ProdutoDto>>("/api/v1/produtos?busca=CE-100"))!;
        var prazo = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20);
        return new PedidoRequest(cliente.Id, prazo, null, [new PedidoItemRequest(produtos.Items.Single(p => p.Codigo == "CE-100").Id, 1)]);
    }

    private static async Task AssertAcessoAsync(HttpResponseMessage resposta, bool permitido, HttpStatusCode sucesso)
    {
        if (permitido)
        {
            resposta.AssertStatus(sucesso);
            return;
        }

        resposta.AssertStatus(HttpStatusCode.Forbidden);
        Assert.Equal("SEM_PERMISSAO", await resposta.CodigoAsync());
    }

    [Theory]
    [MemberData(nameof(Leitura))]
    public async Task Leitura_de_pedidos(string perfil, bool permitido)
    {
        using var cliente = await api.ComoAsync(perfil);

        var resposta = await cliente.GetAsync("/api/v1/pedidos");

        await AssertAcessoAsync(resposta, permitido, HttpStatusCode.OK);
    }

    [Theory]
    [MemberData(nameof(Escrita))]
    public async Task Criacao_de_pedidos(string perfil, bool permitido)
    {
        using var admin = await api.ComoAdministradorAsync();
        var request = await PedidoValidoAsync(admin);
        using var cliente = await api.ComoAsync(perfil);

        var resposta = await cliente.PostAsJsonAsync("/api/v1/pedidos", request);

        await AssertAcessoAsync(resposta, permitido, HttpStatusCode.Created);
    }

    [Theory]
    [MemberData(nameof(Escrita))]
    public async Task Edicao_e_cancelamento_de_pedidos(string perfil, bool permitido)
    {
        using var admin = await api.ComoAdministradorAsync();
        var request = await PedidoValidoAsync(admin);
        var pedido = (await (await admin.PostAsJsonAsync("/api/v1/pedidos", request)).Content.ReadFromJsonAsync<PedidoDto>())!;
        using var cliente = await api.ComoAsync(perfil);

        var edicao = await cliente.PutAsJsonAsync($"/api/v1/pedidos/{pedido.Id}", request);
        var cancelamento = await cliente.PostAsync($"/api/v1/pedidos/{pedido.Id}/cancelar", null);

        if (permitido && perfil == "Vendedor")
        {
            // O vendedor só mexe nos próprios pedidos: este foi criado pelo administrador
            edicao.AssertStatus(HttpStatusCode.NotFound);
            cancelamento.AssertStatus(HttpStatusCode.NotFound);
            return;
        }

        await AssertAcessoAsync(edicao, permitido, HttpStatusCode.OK);
        await AssertAcessoAsync(cancelamento, permitido, HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/v1/pedidos")]
    [InlineData("/api/v1/pedidos/1")]
    public async Task Rotas_sem_token_devolvem_401(string rota)
    {
        using var cliente = api.CreateClient();

        var resposta = await cliente.GetAsync(rota);

        resposta.AssertStatus(HttpStatusCode.Unauthorized);
    }
}
