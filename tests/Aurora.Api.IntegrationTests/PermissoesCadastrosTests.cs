using System.Net;
using System.Net.Http.Json;
using Aurora.Application.MateriasPrimas;
using Aurora.Application.Produtos;

namespace Aurora.Api.IntegrationTests;

/// <summary>Matriz de permissões (docs/06 §3) de produtos e matérias-primas: 200/201, 401 e 403 por perfil.</summary>
[Collection("api")]
public class PermissoesCadastrosTests(ApiFactory api)
{
    private static readonly string[] Internos =
        ["Administrador", "GerenteProducao", "Operador", "Vendedor", "Almoxarife", "Inspetor"];

    public static TheoryData<string, bool> LeituraDeProdutos => Matriz(
        ["Administrador", "GerenteProducao", "Operador", "Vendedor", "Almoxarife", "Inspetor"]);

    public static TheoryData<string, bool> EscritaDeProdutos => Matriz(["Administrador", "GerenteProducao"]);

    public static TheoryData<string, bool> LeituraDeMateriasPrimas => Matriz(
        ["Administrador", "GerenteProducao", "Operador", "Almoxarife"]);

    public static TheoryData<string, bool> EscritaDeMateriasPrimas => Matriz(["Administrador", "Almoxarife"]);

    private static TheoryData<string, bool> Matriz(string[] permitidos)
    {
        var dados = new TheoryData<string, bool>();
        foreach (var perfil in Internos.Append("Cliente"))
        {
            dados.Add(perfil, permitidos.Contains(perfil));
        }

        return dados;
    }

    private static string Codigo(string prefixo) => (prefixo + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant();

    [Theory]
    [MemberData(nameof(LeituraDeProdutos))]
    public async Task Leitura_de_produtos(string perfil, bool permitido)
    {
        using var cliente = await api.ComoAsync(perfil);

        var resposta = await cliente.GetAsync("/api/v1/produtos");

        await AssertAcessoAsync(resposta, permitido, HttpStatusCode.OK);
    }

    [Theory]
    [MemberData(nameof(EscritaDeProdutos))]
    public async Task Escrita_de_produtos(string perfil, bool permitido)
    {
        using var cliente = await api.ComoAsync(perfil);

        var resposta = await cliente.PostAsJsonAsync("/api/v1/produtos", new ProdutoRequest(Codigo("P"), "Produto", 1, 10m));

        await AssertAcessoAsync(resposta, permitido, HttpStatusCode.Created);
    }

    [Theory]
    [MemberData(nameof(LeituraDeMateriasPrimas))]
    public async Task Leitura_de_materias_primas(string perfil, bool permitido)
    {
        using var cliente = await api.ComoAsync(perfil);

        var resposta = await cliente.GetAsync("/api/v1/materias-primas");

        await AssertAcessoAsync(resposta, permitido, HttpStatusCode.OK);
    }

    [Theory]
    [MemberData(nameof(EscritaDeMateriasPrimas))]
    public async Task Escrita_de_materias_primas(string perfil, bool permitido)
    {
        using var cliente = await api.ComoAsync(perfil);

        var resposta = await cliente.PostAsJsonAsync(
            "/api/v1/materias-primas", new MateriaPrimaRequest(Codigo("M"), "Insumo", "un", 1m, 1m, null));

        await AssertAcessoAsync(resposta, permitido, HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("/api/v1/produtos")]
    [InlineData("/api/v1/produtos/1")]
    [InlineData("/api/v1/materias-primas")]
    [InlineData("/api/v1/materias-primas/1")]
    public async Task Rotas_sem_token_devolvem_401(string rota)
    {
        using var cliente = api.CreateClient();

        var resposta = await cliente.GetAsync(rota);

        resposta.AssertStatus(HttpStatusCode.Unauthorized);
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
}
