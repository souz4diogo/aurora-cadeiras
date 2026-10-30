using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Comum;
using Aurora.Application.Produtos;
using Microsoft.Data.SqlClient;

namespace Aurora.Api.IntegrationTests;

[Collection("api")]
public class ProdutosTests(ApiFactory api)
{
    private static string NovoCodigo() => ("T" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant();

    private static ProdutoRequest NovoRequest(string? codigo = null) =>
        new(codigo ?? NovoCodigo(), "Cadeira de Teste", 1, 350.75m);

    private static async Task<ProdutoDto> CriarAsync(HttpClient cliente, ProdutoRequest? request = null)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/v1/produtos", request ?? NovoRequest());
        resposta.AssertStatus(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<ProdutoDto>())!;
    }

    [Fact]
    public async Task Criar_devolve_201_com_location_e_codigo_em_maiusculas()
    {
        using var gerente = await api.ComoAsync("GerenteProducao");
        var codigo = NovoCodigo().ToLowerInvariant();

        var resposta = await gerente.PostAsJsonAsync("/api/v1/produtos", NovoRequest(codigo));

        resposta.AssertStatus(HttpStatusCode.Created);
        var criado = (await resposta.Content.ReadFromJsonAsync<ProdutoDto>())!;
        Assert.Equal(codigo.ToUpperInvariant(), criado.Codigo);
        Assert.Equal("Escritorio", criado.LinhaProduto);
        Assert.Equal(350.75m, criado.PrecoVenda);
        Assert.True(criado.Ativo);
        Assert.EndsWith($"/api/v1/produtos/{criado.Id}", resposta.Headers.Location?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Criar_grava_a_linha_e_o_preco_no_banco()
    {
        using var admin = await api.ComoAdministradorAsync();
        var criado = await CriarAsync(admin, new ProdutoRequest(NovoCodigo(), "Cadeira Jantar", 2, 99.9m));

        await using var conexao = new SqlConnection(api.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT LinhaProdutoId, PrecoVenda FROM dbo.Produto WHERE ProdutoId = @id";
        comando.Parameters.AddWithValue("@id", criado.Id);
        await using var leitor = await comando.ExecuteReaderAsync();

        Assert.True(await leitor.ReadAsync());
        Assert.Equal(2, leitor.GetByte(0));
        Assert.Equal(99.9m, leitor.GetDecimal(1));
    }

    [Fact]
    public async Task Codigo_duplicado_devolve_409()
    {
        using var admin = await api.ComoAdministradorAsync();
        var existente = await CriarAsync(admin);

        var resposta = await admin.PostAsJsonAsync("/api/v1/produtos", NovoRequest(existente.Codigo.ToLowerInvariant()));

        resposta.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("CODIGO_DUPLICADO", await resposta.CodigoAsync());
    }

    [Theory]
    [InlineData("", "Nome", (byte)1, 10)]
    [InlineData("COD 1", "Nome", (byte)1, 10)]
    [InlineData("COD-1", "", (byte)1, 10)]
    [InlineData("COD-1", "Nome", (byte)9, 10)]
    [InlineData("COD-1", "Nome", (byte)1, -5)]
    public async Task Entrada_invalida_devolve_400(string codigo, string nome, byte linha, int preco)
    {
        using var admin = await api.ComoAdministradorAsync();

        var resposta = await admin.PostAsJsonAsync("/api/v1/produtos", new ProdutoRequest(codigo, nome, linha, preco));

        resposta.AssertStatus(HttpStatusCode.BadRequest);
        Assert.Equal("VALIDACAO", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Obter_inexistente_devolve_404()
    {
        using var admin = await api.ComoAdministradorAsync();

        var resposta = await admin.GetAsync("/api/v1/produtos/987654");

        resposta.AssertStatus(HttpStatusCode.NotFound);
        Assert.Equal("PRODUTO_NAO_ENCONTRADO", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Atualizar_altera_os_dados_e_permite_manter_o_proprio_codigo()
    {
        using var gerente = await api.ComoAsync("GerenteProducao");
        var criado = await CriarAsync(gerente);

        var resposta = await gerente.PutAsJsonAsync(
            $"/api/v1/produtos/{criado.Id}", new ProdutoRequest(criado.Codigo, "Nome Novo", 3, 800m));

        resposta.AssertStatus(HttpStatusCode.OK);
        var atualizado = (await resposta.Content.ReadFromJsonAsync<ProdutoDto>())!;
        Assert.Equal("Nome Novo", atualizado.Nome);
        Assert.Equal("Estofada", atualizado.LinhaProduto);
        Assert.Equal(800m, atualizado.PrecoVenda);
    }

    [Fact]
    public async Task Atualizar_para_codigo_de_outro_produto_devolve_409()
    {
        using var admin = await api.ComoAdministradorAsync();
        var primeiro = await CriarAsync(admin);
        var segundo = await CriarAsync(admin);

        var resposta = await admin.PutAsJsonAsync(
            $"/api/v1/produtos/{segundo.Id}", new ProdutoRequest(primeiro.Codigo, "Nome", 1, 10m));

        resposta.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("CODIGO_DUPLICADO", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Inativar_e_reativar_altera_o_filtro_de_ativos()
    {
        using var admin = await api.ComoAdministradorAsync();
        var criado = await CriarAsync(admin);

        (await admin.PatchAsJsonAsync($"/api/v1/produtos/{criado.Id}/ativo", new AlterarAtivoRequest(false)))
            .AssertStatus(HttpStatusCode.OK);
        var ativos = await admin.GetFromJsonAsync<Pagina<ProdutoDto>>($"/api/v1/produtos?busca={criado.Codigo}&ativo=true");
        var inativos = await admin.GetFromJsonAsync<Pagina<ProdutoDto>>($"/api/v1/produtos?busca={criado.Codigo}&ativo=false");

        Assert.Empty(ativos!.Items);
        Assert.Single(inativos!.Items);
        (await admin.PatchAsJsonAsync($"/api/v1/produtos/{criado.Id}/ativo", new AlterarAtivoRequest(true)))
            .AssertStatus(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Listar_pagina_e_busca_por_codigo_ou_nome()
    {
        using var admin = await api.ComoAdministradorAsync();

        var porCodigo = await admin.GetFromJsonAsync<Pagina<ProdutoDto>>("/api/v1/produtos?busca=CE-100");
        var paginado = await admin.GetFromJsonAsync<Pagina<ProdutoDto>>("/api/v1/produtos?page=1&pageSize=2");

        Assert.Contains(porCodigo!.Items, p => p.Codigo == "CE-100");
        Assert.Equal(2, paginado!.Items.Count);
        Assert.True(paginado.Total >= 6);
        Assert.Equal(2, paginado.PageSize);
    }

    [Fact]
    public async Task Tamanho_de_pagina_e_limitado_a_100()
    {
        using var admin = await api.ComoAdministradorAsync();

        var pagina = await admin.GetFromJsonAsync<Pagina<ProdutoDto>>("/api/v1/produtos?pageSize=5000");

        Assert.Equal(Paginacao.TamanhoMaximo, pagina!.PageSize);
    }
}
