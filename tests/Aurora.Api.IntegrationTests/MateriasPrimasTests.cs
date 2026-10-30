using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Comum;
using Aurora.Application.MateriasPrimas;
using Microsoft.Data.SqlClient;

namespace Aurora.Api.IntegrationTests;

[Collection("api")]
public class MateriasPrimasTests(ApiFactory api)
{
    private static string NovoCodigo() => ("M" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant();

    private static MateriaPrimaRequest NovoRequest(string? codigo = null, int? fornecedorId = null) =>
        new(codigo ?? NovoCodigo(), "Madeira de Teste", "m3", 2.5m, 4500.1234m, fornecedorId);

    private static async Task<MateriaPrimaDto> CriarAsync(HttpClient cliente, MateriaPrimaRequest? request = null)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/v1/materias-primas", request ?? NovoRequest());
        resposta.AssertStatus(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<MateriaPrimaDto>())!;
    }

    [Fact]
    public async Task Criar_devolve_201_com_saldo_zerado_e_unidade_em_codigo()
    {
        using var almoxarife = await api.ComoAsync("Almoxarife");

        var resposta = await almoxarife.PostAsJsonAsync("/api/v1/materias-primas", NovoRequest(fornecedorId: 1));

        resposta.AssertStatus(HttpStatusCode.Created);
        var criada = (await resposta.Content.ReadFromJsonAsync<MateriaPrimaDto>())!;
        Assert.Equal("m3", criada.Unidade);
        Assert.Equal(0m, criada.EstoqueAtual);
        Assert.Equal(2.5m, criada.EstoqueMinimo);
        Assert.Equal(4500.1234m, criada.CustoUnitario);
        Assert.Equal(1, criada.FornecedorPreferencialId);
        Assert.True(criada.Ativo);
        Assert.EndsWith($"/api/v1/materias-primas/{criada.Id}", resposta.Headers.Location?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Criar_grava_a_unidade_como_codigo_do_banco_e_gera_auditoria()
    {
        using var admin = await api.ComoAdministradorAsync();
        var criada = await CriarAsync(admin, new MateriaPrimaRequest(NovoCodigo(), "Tecido", "m2", 1m, 10m, null));

        await using var conexao = new SqlConnection(api.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            "SELECT (SELECT Unidade FROM dbo.MateriaPrima WHERE MateriaPrimaId = @id), " +
            "(SELECT COUNT(*) FROM dbo.Auditoria WHERE Tabela = N'MateriaPrima' AND Registro = CAST(@id AS varchar(50)) AND Acao = 'I')";
        comando.Parameters.AddWithValue("@id", criada.Id);
        await using var leitor = await comando.ExecuteReaderAsync();

        Assert.True(await leitor.ReadAsync());
        Assert.Equal("m2", leitor.GetString(0));
        Assert.Equal(1, leitor.GetInt32(1));
    }

    [Fact]
    public async Task Codigo_duplicado_devolve_409()
    {
        using var admin = await api.ComoAdministradorAsync();
        var existente = await CriarAsync(admin);

        var resposta = await admin.PostAsJsonAsync("/api/v1/materias-primas", NovoRequest(existente.Codigo));

        resposta.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("CODIGO_DUPLICADO", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Fornecedor_inexistente_devolve_422()
    {
        using var admin = await api.ComoAdministradorAsync();

        var resposta = await admin.PostAsJsonAsync("/api/v1/materias-primas", NovoRequest(fornecedorId: 999999));

        resposta.AssertStatus(HttpStatusCode.UnprocessableEntity);
        Assert.Equal("FORNECEDOR_INEXISTENTE", await resposta.CodigoAsync());
    }

    [Theory]
    [InlineData("kg", 1, 1)]
    [InlineData("m", -1, 1)]
    [InlineData("m", 1, -1)]
    [InlineData("m", 1.0001, 1)]
    public async Task Entrada_invalida_devolve_400(string unidade, double minimo, double custo)
    {
        using var admin = await api.ComoAdministradorAsync();
        var request = new MateriaPrimaRequest(NovoCodigo(), "Item", unidade, (decimal)minimo, (decimal)custo, null);

        var resposta = await admin.PostAsJsonAsync("/api/v1/materias-primas", request);

        resposta.AssertStatus(HttpStatusCode.BadRequest);
        Assert.Equal("VALIDACAO", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Obter_inexistente_devolve_404()
    {
        using var admin = await api.ComoAdministradorAsync();

        var resposta = await admin.GetAsync("/api/v1/materias-primas/987654");

        resposta.AssertStatus(HttpStatusCode.NotFound);
        Assert.Equal("MATERIA_PRIMA_NAO_ENCONTRADA", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Atualizar_preserva_o_saldo_vindo_dos_movimentos_de_estoque()
    {
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var antes = (await almoxarife.GetFromJsonAsync<Pagina<MateriaPrimaDto>>("/api/v1/materias-primas?busca=MP-001"))!
            .Items.Single(m => m.Codigo == "MP-001");
        Assert.True(antes.EstoqueAtual > 0, "o seed deve ter saldo inicial em MP-001");

        var resposta = await almoxarife.PutAsJsonAsync(
            $"/api/v1/materias-primas/{antes.Id}",
            new MateriaPrimaRequest(antes.Codigo, antes.Nome, "m3", antes.EstoqueMinimo + 1, antes.CustoUnitario, antes.FornecedorPreferencialId));

        resposta.AssertStatus(HttpStatusCode.OK);
        var depois = (await resposta.Content.ReadFromJsonAsync<MateriaPrimaDto>())!;
        Assert.Equal(antes.EstoqueAtual, depois.EstoqueAtual);
        Assert.Equal(antes.EstoqueMinimo + 1, depois.EstoqueMinimo);
        var recarregada = (await almoxarife.GetFromJsonAsync<MateriaPrimaDto>($"/api/v1/materias-primas/{antes.Id}"))!;
        Assert.Equal(antes.EstoqueAtual, recarregada.EstoqueAtual);
    }

    [Fact]
    public async Task Inativar_altera_o_filtro_de_ativas()
    {
        using var admin = await api.ComoAdministradorAsync();
        var criada = await CriarAsync(admin);

        (await admin.PatchAsJsonAsync($"/api/v1/materias-primas/{criada.Id}/ativo", new AlterarAtivoRequest(false)))
            .AssertStatus(HttpStatusCode.OK);
        var ativas = await admin.GetFromJsonAsync<Pagina<MateriaPrimaDto>>($"/api/v1/materias-primas?busca={criada.Codigo}&ativo=true");
        var inativas = await admin.GetFromJsonAsync<Pagina<MateriaPrimaDto>>($"/api/v1/materias-primas?busca={criada.Codigo}&ativo=false");

        Assert.Empty(ativas!.Items);
        Assert.Single(inativas!.Items);
    }
}
