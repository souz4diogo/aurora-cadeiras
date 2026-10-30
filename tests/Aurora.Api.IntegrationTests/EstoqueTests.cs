using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Comum;
using Aurora.Application.Estoque;
using Aurora.Application.MateriasPrimas;
using Microsoft.Data.SqlClient;

namespace Aurora.Api.IntegrationTests;

[Collection("api")]
public class EstoqueTests(ApiFactory api)
{
    private static async Task<MateriaPrimaDto> NovaMateriaPrimaAsync(HttpClient cliente, decimal estoqueMinimo = 10m)
    {
        var codigo = ("E" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant();
        var resposta = await cliente.PostAsJsonAsync(
            "/api/v1/materias-primas", new MateriaPrimaRequest(codigo, "Insumo de Estoque", "m2", estoqueMinimo, 12.5m, null));
        resposta.AssertStatus(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<MateriaPrimaDto>())!;
    }

    private static Task<HttpResponseMessage> AjustarAsync(HttpClient cliente, int materiaPrimaId, decimal quantidade, string motivo = "Contagem física") =>
        cliente.PostAsJsonAsync("/api/v1/estoque/ajustes", new AjusteDeEstoqueRequest(materiaPrimaId, quantidade, motivo));

    private static Task<Pagina<MovimentoEstoqueDto>?> MovimentosAsync(HttpClient cliente, string consulta) =>
        cliente.GetFromJsonAsync<Pagina<MovimentoEstoqueDto>>($"/api/v1/estoque/movimentos?{consulta}");

    private async Task<int> ContarAsync(string sql, int materiaPrimaId)
    {
        await using var conexao = new SqlConnection(api.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        comando.Parameters.AddWithValue("@id", materiaPrimaId);
        return (int)(await comando.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Ajustar_atualiza_o_saldo_e_registra_a_diferenca_como_movimento_de_ajuste()
    {
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var materia = await NovaMateriaPrimaAsync(almoxarife);

        var resposta = await AjustarAsync(almoxarife, materia.Id, 25.5m, "Contagem inicial");

        resposta.AssertStatus(HttpStatusCode.OK);
        var ajuste = (await resposta.Content.ReadFromJsonAsync<AjusteDeEstoqueDto>())!;
        Assert.Equal(0m, ajuste.SaldoAnterior);
        Assert.Equal(25.5m, ajuste.Diferenca);
        Assert.Equal(25.5m, ajuste.MateriaPrima.EstoqueAtual);

        var extrato = (await MovimentosAsync(almoxarife, $"materiaPrimaId={materia.Id}"))!;
        var movimento = Assert.Single(extrato.Items);
        Assert.Equal("Ajuste", movimento.Tipo);
        Assert.Equal("Ajuste", movimento.Origem);
        Assert.Equal(25.5m, movimento.Quantidade);
        Assert.Equal("Contagem inicial", movimento.Motivo);
        Assert.Equal("MateriaPrima", movimento.ItemTipo);
        Assert.Equal(materia.Id, movimento.ItemId);
        Assert.Equal(materia.Codigo, movimento.ItemCodigo);
        Assert.Equal("Usuário Almoxarife", movimento.UsuarioNome);
        Assert.True(movimento.DataHora > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task Ajustar_para_baixo_gera_movimento_negativo_e_alerta_de_estoque_minimo()
    {
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var materia = await NovaMateriaPrimaAsync(almoxarife, estoqueMinimo: 10m);
        (await AjustarAsync(almoxarife, materia.Id, 30m)).AssertStatus(HttpStatusCode.OK);
        var alertasAntes = await ContarAsync("SELECT COUNT(*) FROM dbo.AlertaEstoque WHERE MateriaPrimaId = @id", materia.Id);

        var resposta = await AjustarAsync(almoxarife, materia.Id, 8m, "Perda por umidade");

        var ajuste = (await resposta.Content.ReadFromJsonAsync<AjusteDeEstoqueDto>())!;
        Assert.Equal(30m, ajuste.SaldoAnterior);
        Assert.Equal(-22m, ajuste.Diferenca);
        var extrato = (await MovimentosAsync(almoxarife, $"materiaPrimaId={materia.Id}"))!;
        Assert.Equal([-22m, 30m], extrato.Items.Select(i => i.Quantidade));
        Assert.Equal(0, alertasAntes);
        Assert.Equal(1, await ContarAsync("SELECT COUNT(*) FROM dbo.AlertaEstoque WHERE MateriaPrimaId = @id", materia.Id));
    }

    [Fact]
    public async Task Ajustar_para_o_mesmo_saldo_nao_cria_movimento()
    {
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var materia = await NovaMateriaPrimaAsync(almoxarife);
        (await AjustarAsync(almoxarife, materia.Id, 5m)).AssertStatus(HttpStatusCode.OK);

        var resposta = await AjustarAsync(almoxarife, materia.Id, 5m, "Conferência sem diferença");

        var ajuste = (await resposta.Content.ReadFromJsonAsync<AjusteDeEstoqueDto>())!;
        Assert.Equal(0m, ajuste.Diferenca);
        Assert.Equal(1, (await MovimentosAsync(almoxarife, $"materiaPrimaId={materia.Id}"))!.Total);
    }

    [Fact]
    public async Task Ajuste_invalido_devolve_400_e_nao_altera_o_saldo()
    {
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var materia = await NovaMateriaPrimaAsync(almoxarife);

        var semMotivo = await AjustarAsync(almoxarife, materia.Id, 3m, "  ");
        var negativo = await AjustarAsync(almoxarife, materia.Id, -1m);
        var casasDemais = await AjustarAsync(almoxarife, materia.Id, 1.0001m);

        foreach (var resposta in new[] { semMotivo, negativo, casasDemais })
        {
            resposta.AssertStatus(HttpStatusCode.BadRequest);
            Assert.Equal("VALIDACAO", await resposta.CodigoAsync());
        }

        Assert.Equal(0, (await MovimentosAsync(almoxarife, $"materiaPrimaId={materia.Id}"))!.Total);
        var atual = await almoxarife.GetFromJsonAsync<MateriaPrimaDto>($"/api/v1/materias-primas/{materia.Id}");
        Assert.Equal(0m, atual!.EstoqueAtual);
    }

    [Fact]
    public async Task Ajustar_materia_prima_inexistente_devolve_404()
    {
        using var almoxarife = await api.ComoAsync("Almoxarife");

        var resposta = await AjustarAsync(almoxarife, 987654, 1m);

        resposta.AssertStatus(HttpStatusCode.NotFound);
        Assert.Equal("MATERIA_PRIMA_NAO_ENCONTRADA", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Extrato_filtra_por_tipo_origem_periodo_e_item_e_ordena_do_mais_recente()
    {
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var materia = await NovaMateriaPrimaAsync(almoxarife);
        var outra = await NovaMateriaPrimaAsync(almoxarife);
        (await AjustarAsync(almoxarife, materia.Id, 10m)).AssertStatus(HttpStatusCode.OK);
        (await AjustarAsync(almoxarife, materia.Id, 4m)).AssertStatus(HttpStatusCode.OK);
        (await AjustarAsync(almoxarife, outra.Id, 99m)).AssertStatus(HttpStatusCode.OK);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);

        var daMateria = await MovimentosAsync(almoxarife, $"materiaPrimaId={materia.Id}");
        var ajustes = await MovimentosAsync(almoxarife, $"materiaPrimaId={materia.Id}&tipo=Ajuste&origem=Ajuste");
        var entradas = await MovimentosAsync(almoxarife, $"materiaPrimaId={materia.Id}&tipo=Entrada");
        var hojeNoPeriodo = await MovimentosAsync(almoxarife, $"materiaPrimaId={materia.Id}&de={hoje:yyyy-MM-dd}&ate={hoje:yyyy-MM-dd}");
        var amanha = await MovimentosAsync(almoxarife, $"materiaPrimaId={materia.Id}&de={hoje.AddDays(1):yyyy-MM-dd}");
        var deProduto = await MovimentosAsync(almoxarife, $"produtoId=987654&materiaPrimaId={materia.Id}");

        Assert.Equal([-6m, 10m], daMateria!.Items.Select(i => i.Quantidade));
        Assert.Equal(2, ajustes!.Total);
        Assert.Empty(entradas!.Items);
        Assert.Equal(2, hojeNoPeriodo!.Total);
        Assert.Empty(amanha!.Items);
        Assert.Empty(deProduto!.Items);
    }

    [Fact]
    public async Task Extrato_do_seed_traz_o_saldo_inicial_das_materias_primas_com_o_nome_do_item()
    {
        using var gerente = await api.ComoAsync("GerenteProducao");

        var extrato = (await MovimentosAsync(gerente, "pageSize=100"))!;

        Assert.NotEmpty(extrato.Items);
        Assert.All(extrato.Items, m =>
        {
            Assert.False(string.IsNullOrWhiteSpace(m.ItemCodigo));
            Assert.False(string.IsNullOrWhiteSpace(m.ItemNome));
            Assert.True(m.ItemTipo is "MateriaPrima" or "Produto");
        });
        Assert.Equal(extrato.Items.OrderByDescending(m => m.DataHora).ThenByDescending(m => m.Id).Select(m => m.Id), extrato.Items.Select(m => m.Id));
    }

    [Fact]
    public async Task Extrato_pagina_e_limita_o_tamanho()
    {
        using var admin = await api.ComoAdministradorAsync();

        var pagina = await MovimentosAsync(admin, "pageSize=5000");
        var umPorPagina = await MovimentosAsync(admin, "pageSize=1&page=2");

        Assert.Equal(Paginacao.TamanhoMaximo, pagina!.PageSize);
        Assert.Single(umPorPagina!.Items);
        Assert.Equal(2, umPorPagina.Page);
    }

    [Fact]
    public async Task Tipo_invalido_na_consulta_devolve_400()
    {
        using var admin = await api.ComoAdministradorAsync();

        var resposta = await admin.GetAsync("/api/v1/estoque/movimentos?tipo=Inexistente");

        resposta.AssertStatus(HttpStatusCode.BadRequest);
    }
}
