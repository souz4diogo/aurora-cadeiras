using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Comum;
using Aurora.Application.FichasTecnicas;
using Aurora.Application.MateriasPrimas;
using Aurora.Application.Produtos;
using Aurora.Application.Roteiros;

namespace Aurora.Api.IntegrationTests;

[Collection("api")]
public class FichaTecnicaTests(ApiFactory api)
{
    private static Task<HttpResponseMessage> SubstituirAsync(HttpClient cliente, int produtoId, params FichaTecnicaItemRequest[] itens) =>
        cliente.PutAsJsonAsync($"/api/v1/produtos/{produtoId}/ficha-tecnica", new FichaTecnicaRequest(itens));

    /// <summary>Produto novo com roteiro Corte, Estrutura e Qualidade, para a ficha ter onde consumir material.</summary>
    private static async Task<ProdutoDto> ProdutoComRoteiroAsync(HttpClient admin)
    {
        var produto = await RoteiroTests.CriarProdutoAsync(admin);
        (await admin.PutAsJsonAsync($"/api/v1/produtos/{produto.Id}/roteiro", new RoteiroRequest(new byte[] { 1, 2, 5 })))
            .AssertStatus(HttpStatusCode.OK);
        return produto;
    }

    private static async Task<List<MateriaPrimaDto>> MateriasDoSeedAsync(HttpClient admin) =>
        (await admin.GetFromJsonAsync<Pagina<MateriaPrimaDto>>("/api/v1/materias-primas?pageSize=100"))!
            .Items.OrderBy(m => m.Id).ToList();

    [Fact]
    public async Task Ficha_do_seed_traz_nome_unidade_e_estacao_de_cada_material()
    {
        using var admin = await api.ComoAdministradorAsync();
        var produtos = await admin.GetFromJsonAsync<Pagina<ProdutoDto>>("/api/v1/produtos?busca=CE-100");
        var id = produtos!.Items.Single(p => p.Codigo == "CE-100").Id;

        var ficha = await admin.GetFromJsonAsync<FichaTecnicaDto>($"/api/v1/produtos/{id}/ficha-tecnica");

        Assert.NotEmpty(ficha!.Itens);
        Assert.All(ficha.Itens, i =>
        {
            Assert.False(string.IsNullOrWhiteSpace(i.MateriaPrimaNome));
            Assert.False(string.IsNullOrWhiteSpace(i.Estacao));
            Assert.True(i.Quantidade > 0);
        });
        Assert.Equal(ficha.Itens.OrderBy(i => i.EstacaoId).Select(i => i.EstacaoId), ficha.Itens.Select(i => i.EstacaoId));
    }

    [Fact]
    public async Task Substituir_grava_os_itens_e_devolve_a_ficha_completa()
    {
        using var gerente = await api.ComoAsync("GerenteProducao");
        var produto = await ProdutoComRoteiroAsync(gerente);
        var materias = await MateriasDoSeedAsync(gerente);

        var resposta = await SubstituirAsync(gerente, produto.Id,
            new FichaTecnicaItemRequest(materias[1].Id, 2, 4.5m),
            new FichaTecnicaItemRequest(materias[0].Id, 1, 0.025m));

        resposta.AssertStatus(HttpStatusCode.OK);
        var ficha = (await resposta.Content.ReadFromJsonAsync<FichaTecnicaDto>())!;
        Assert.Equal(2, ficha.Itens.Count);
        Assert.Equal((byte)1, ficha.Itens[0].EstacaoId);
        Assert.Equal(materias[0].Codigo, ficha.Itens[0].MateriaPrimaCodigo);
        Assert.Equal(0.025m, ficha.Itens[0].Quantidade);
        Assert.Equal(materias[0].Unidade, ficha.Itens[0].Unidade);
        Assert.Equal((byte)2, ficha.Itens[1].EstacaoId);
    }

    [Fact]
    public async Task Substituir_de_novo_troca_tudo_e_lista_vazia_limpa_a_ficha()
    {
        using var admin = await api.ComoAdministradorAsync();
        var produto = await ProdutoComRoteiroAsync(admin);
        var materias = await MateriasDoSeedAsync(admin);
        (await SubstituirAsync(admin, produto.Id,
            new FichaTecnicaItemRequest(materias[0].Id, 1, 1m),
            new FichaTecnicaItemRequest(materias[1].Id, 2, 2m))).AssertStatus(HttpStatusCode.OK);

        var troca = await SubstituirAsync(admin, produto.Id, new FichaTecnicaItemRequest(materias[0].Id, 1, 3m));
        var limpa = await SubstituirAsync(admin, produto.Id);

        var depoisDaTroca = (await troca.Content.ReadFromJsonAsync<FichaTecnicaDto>())!;
        Assert.Single(depoisDaTroca.Itens);
        Assert.Equal(3m, depoisDaTroca.Itens[0].Quantidade);
        limpa.AssertStatus(HttpStatusCode.OK);
        Assert.Empty((await limpa.Content.ReadFromJsonAsync<FichaTecnicaDto>())!.Itens);
    }

    [Fact]
    public async Task Mesmo_material_em_estacoes_diferentes_e_permitido()
    {
        using var admin = await api.ComoAdministradorAsync();
        var produto = await ProdutoComRoteiroAsync(admin);
        var materia = (await MateriasDoSeedAsync(admin))[0];

        var resposta = await SubstituirAsync(admin, produto.Id,
            new FichaTecnicaItemRequest(materia.Id, 1, 1m),
            new FichaTecnicaItemRequest(materia.Id, 2, 1m));

        resposta.AssertStatus(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Estacao_fora_do_roteiro_devolve_422_e_preserva_a_ficha_anterior()
    {
        using var admin = await api.ComoAdministradorAsync();
        var produto = await ProdutoComRoteiroAsync(admin);
        var materia = (await MateriasDoSeedAsync(admin))[0];
        (await SubstituirAsync(admin, produto.Id, new FichaTecnicaItemRequest(materia.Id, 1, 1m))).AssertStatus(HttpStatusCode.OK);

        var resposta = await SubstituirAsync(admin, produto.Id,
            new FichaTecnicaItemRequest(materia.Id, 2, 9m),
            new FichaTecnicaItemRequest(materia.Id, 4, 9m)); // Montagem Final não está no roteiro

        resposta.AssertStatus(HttpStatusCode.UnprocessableEntity);
        Assert.Equal("ESTACAO_FORA_DO_ROTEIRO", await resposta.CodigoAsync());
        var ficha = await admin.GetFromJsonAsync<FichaTecnicaDto>($"/api/v1/produtos/{produto.Id}/ficha-tecnica");
        Assert.Single(ficha!.Itens);
        Assert.Equal(1m, ficha.Itens[0].Quantidade);
    }

    [Fact]
    public async Task Materia_prima_inexistente_devolve_422()
    {
        using var admin = await api.ComoAdministradorAsync();
        var produto = await ProdutoComRoteiroAsync(admin);

        var resposta = await SubstituirAsync(admin, produto.Id, new FichaTecnicaItemRequest(987654, 1, 1m));

        resposta.AssertStatus(HttpStatusCode.UnprocessableEntity);
        Assert.Equal("MATERIA_PRIMA_INEXISTENTE", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Entrada_invalida_devolve_400()
    {
        using var admin = await api.ComoAdministradorAsync();
        var produto = await ProdutoComRoteiroAsync(admin);
        var materia = (await MateriasDoSeedAsync(admin))[0];

        var quantidadeZero = await SubstituirAsync(admin, produto.Id, new FichaTecnicaItemRequest(materia.Id, 1, 0m));
        var casasDemais = await SubstituirAsync(admin, produto.Id, new FichaTecnicaItemRequest(materia.Id, 1, 1.0001m));
        var repetido = await SubstituirAsync(admin, produto.Id,
            new FichaTecnicaItemRequest(materia.Id, 1, 1m),
            new FichaTecnicaItemRequest(materia.Id, 1, 2m));

        foreach (var resposta in new[] { quantidadeZero, casasDemais, repetido })
        {
            resposta.AssertStatus(HttpStatusCode.BadRequest);
            Assert.Equal("VALIDACAO", await resposta.CodigoAsync());
        }
    }

    [Fact]
    public async Task Retirar_do_roteiro_uma_estacao_usada_pela_ficha_devolve_422_e_preserva_o_roteiro()
    {
        using var admin = await api.ComoAdministradorAsync();
        var produto = await ProdutoComRoteiroAsync(admin);
        var materia = (await MateriasDoSeedAsync(admin))[0];
        (await SubstituirAsync(admin, produto.Id, new FichaTecnicaItemRequest(materia.Id, 2, 1m))).AssertStatus(HttpStatusCode.OK);

        var resposta = await admin.PutAsJsonAsync($"/api/v1/produtos/{produto.Id}/roteiro", new RoteiroRequest(new byte[] { 1, 5 }));

        resposta.AssertStatus(HttpStatusCode.UnprocessableEntity);
        Assert.Equal("ROTEIRO_INCOMPATIVEL_COM_FICHA", await resposta.CodigoAsync());
        var roteiro = await admin.GetFromJsonAsync<RoteiroDto>($"/api/v1/produtos/{produto.Id}/roteiro");
        Assert.Equal([1, 2, 5], roteiro!.Etapas.Select(e => (int)e.EstacaoId));
    }

    [Fact]
    public async Task Produto_inexistente_devolve_404()
    {
        using var admin = await api.ComoAdministradorAsync();

        var leitura = await admin.GetAsync("/api/v1/produtos/987654/ficha-tecnica");
        var escrita = await SubstituirAsync(admin, 987654);

        leitura.AssertStatus(HttpStatusCode.NotFound);
        escrita.AssertStatus(HttpStatusCode.NotFound);
    }
}
