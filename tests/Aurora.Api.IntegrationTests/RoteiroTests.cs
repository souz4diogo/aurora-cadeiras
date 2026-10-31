using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Estacoes;
using Aurora.Application.Produtos;
using Aurora.Application.Roteiros;
using Microsoft.Data.SqlClient;

namespace Aurora.Api.IntegrationTests;

[Collection("api")]
public class RoteiroTests(ApiFactory api)
{
    /// <summary>Cria um produto novo (sem roteiro nem ficha) para o teste não mexer nos dados do seed.</summary>
    internal static async Task<ProdutoDto> CriarProdutoAsync(HttpClient admin)
    {
        var codigo = ("R" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant();
        var resposta = await admin.PostAsJsonAsync("/api/v1/produtos", new ProdutoRequest(codigo, "Produto de Roteiro", 1, 100m));
        resposta.AssertStatus(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<ProdutoDto>())!;
    }

    private static Task<HttpResponseMessage> SubstituirAsync(HttpClient cliente, int produtoId, params byte[] estacoes) =>
        cliente.PutAsJsonAsync($"/api/v1/produtos/{produtoId}/roteiro", new RoteiroRequest(estacoes));

    [Fact]
    public async Task Estacoes_vem_na_ordem_do_layout_e_a_expedicao_nao_gera_etapa()
    {
        using var operador = await api.ComoAsync("Operador");

        var estacoes = await operador.GetFromJsonAsync<List<EstacaoDto>>("/api/v1/estacoes");

        Assert.Equal(6, estacoes!.Count);
        Assert.Equal(["Corte", "Montagem da Estrutura", "Estofaria", "Montagem Final", "Qualidade", "Expedicao"], estacoes.Select(e => e.Nome));
        Assert.Equal((byte)6, estacoes[^1].Id);
        Assert.False(estacoes[^1].GeraEtapa);
        Assert.All(estacoes.Take(5), e => Assert.True(e.GeraEtapa));
    }

    [Fact]
    public async Task Roteiro_do_seed_de_um_produto_de_escritorio_tem_as_cinco_etapas_em_ordem()
    {
        using var admin = await api.ComoAdministradorAsync();
        var produtos = await admin.GetFromJsonAsync<Aurora.Application.Comum.Pagina<ProdutoDto>>("/api/v1/produtos?busca=CE-100");
        var id = produtos!.Items.Single(p => p.Codigo == "CE-100").Id;

        var roteiro = await admin.GetFromJsonAsync<RoteiroDto>($"/api/v1/produtos/{id}/roteiro");

        Assert.Equal(id, roteiro!.ProdutoId);
        Assert.Equal([1, 2, 3, 4, 5], roteiro.Etapas.Select(e => (int)e.Sequencia));
        Assert.Equal(["Corte", "Montagem da Estrutura", "Estofaria", "Montagem Final", "Qualidade"], roteiro.Etapas.Select(e => e.Estacao));
    }

    [Fact]
    public async Task Produto_novo_comeca_sem_roteiro()
    {
        using var gerente = await api.ComoAsync("GerenteProducao");
        var produto = await CriarProdutoAsync(gerente);

        var roteiro = await gerente.GetFromJsonAsync<RoteiroDto>($"/api/v1/produtos/{produto.Id}/roteiro");

        Assert.Empty(roteiro!.Etapas);
    }

    [Fact]
    public async Task Substituir_define_a_sequencia_pela_ordem_da_lista()
    {
        using var gerente = await api.ComoAsync("GerenteProducao");
        var produto = await CriarProdutoAsync(gerente);

        var resposta = await SubstituirAsync(gerente, produto.Id, 1, 2, 4, 5);

        resposta.AssertStatus(HttpStatusCode.OK);
        var roteiro = (await resposta.Content.ReadFromJsonAsync<RoteiroDto>())!;
        Assert.Equal([(1, 1), (2, 2), (3, 4), (4, 5)], roteiro.Etapas.Select(e => ((int)e.Sequencia, (int)e.EstacaoId)));
    }

    [Fact]
    public async Task Substituir_de_novo_troca_tudo_e_aceita_inverter_a_ordem_das_estacoes()
    {
        using var admin = await api.ComoAdministradorAsync();
        var produto = await CriarProdutoAsync(admin);
        (await SubstituirAsync(admin, produto.Id, 1, 2, 3)).AssertStatus(HttpStatusCode.OK);

        var resposta = await SubstituirAsync(admin, produto.Id, 3, 2);

        resposta.AssertStatus(HttpStatusCode.OK);
        var roteiro = (await resposta.Content.ReadFromJsonAsync<RoteiroDto>())!;
        Assert.Equal([(1, 3), (2, 2)], roteiro.Etapas.Select(e => ((int)e.Sequencia, (int)e.EstacaoId)));

        await using var conexao = new SqlConnection(api.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM dbo.RoteiroProduto WHERE ProdutoId = @id";
        comando.Parameters.AddWithValue("@id", produto.Id);
        Assert.Equal(2, (int)(await comando.ExecuteScalarAsync())!);
    }

    [Theory]
    [InlineData((byte)6)]
    [InlineData((byte)99)]
    public async Task Estacao_inexistente_ou_que_nao_gera_etapa_devolve_422(byte estacao)
    {
        using var admin = await api.ComoAdministradorAsync();
        var produto = await CriarProdutoAsync(admin);

        var resposta = await SubstituirAsync(admin, produto.Id, 1, estacao);

        resposta.AssertStatus(HttpStatusCode.UnprocessableEntity);
        Assert.Equal("ESTACAO_INVALIDA", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Roteiro_vazio_ou_com_estacao_repetida_devolve_400()
    {
        using var admin = await api.ComoAdministradorAsync();
        var produto = await CriarProdutoAsync(admin);

        var vazio = await SubstituirAsync(admin, produto.Id);
        var repetida = await SubstituirAsync(admin, produto.Id, 1, 2, 1);

        vazio.AssertStatus(HttpStatusCode.BadRequest);
        repetida.AssertStatus(HttpStatusCode.BadRequest);
        Assert.Equal("VALIDACAO", await repetida.CodigoAsync());
    }

    [Fact]
    public async Task Roteiro_recusado_nao_altera_o_roteiro_existente()
    {
        using var admin = await api.ComoAdministradorAsync();
        var produto = await CriarProdutoAsync(admin);
        (await SubstituirAsync(admin, produto.Id, 1, 2, 5)).AssertStatus(HttpStatusCode.OK);

        (await SubstituirAsync(admin, produto.Id, 1, 6)).AssertStatus(HttpStatusCode.UnprocessableEntity);

        var roteiro = await admin.GetFromJsonAsync<RoteiroDto>($"/api/v1/produtos/{produto.Id}/roteiro");
        Assert.Equal([1, 2, 5], roteiro!.Etapas.Select(e => (int)e.EstacaoId));
    }

    [Fact]
    public async Task Produto_inexistente_devolve_404()
    {
        using var admin = await api.ComoAdministradorAsync();

        var leitura = await admin.GetAsync("/api/v1/produtos/987654/roteiro");
        var escrita = await SubstituirAsync(admin, 987654, 1);

        leitura.AssertStatus(HttpStatusCode.NotFound);
        escrita.AssertStatus(HttpStatusCode.NotFound);
        Assert.Equal("PRODUTO_NAO_ENCONTRADO", await escrita.CodigoAsync());
    }
}
