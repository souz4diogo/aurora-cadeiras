using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Comum;
using Aurora.Application.Fornecedores;
using Microsoft.Data.SqlClient;

namespace Aurora.Api.IntegrationTests;

[Collection("api")]
public class FornecedoresTests(ApiFactory api)
{
    private static string NovoCnpj() => DadosDeTeste.Digitos(14);

    private static FornecedorRequest NovoRequest(string? cnpj = null, bool comunidade = false) =>
        new("Fornecedor de Teste", cnpj ?? NovoCnpj(), "vendas@fornecedor.local", "(11) 3333-4444", comunidade);

    private static async Task<FornecedorDto> CriarAsync(HttpClient cliente, FornecedorRequest? request = null)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/v1/fornecedores", request ?? NovoRequest());
        resposta.AssertStatus(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<FornecedorDto>())!;
    }

    [Fact]
    public async Task Criar_devolve_201_com_location_e_cnpj_somente_com_digitos()
    {
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var cnpj = NovoCnpj();
        var mascarado = $"{cnpj[..2]}.{cnpj[2..5]}.{cnpj[5..8]}/{cnpj[8..12]}-{cnpj[12..]}";

        var resposta = await almoxarife.PostAsJsonAsync("/api/v1/fornecedores", NovoRequest(mascarado));

        resposta.AssertStatus(HttpStatusCode.Created);
        var criado = (await resposta.Content.ReadFromJsonAsync<FornecedorDto>())!;
        Assert.Equal(cnpj, criado.Cnpj);
        Assert.False(criado.ComunidadeTradicional);
        Assert.True(criado.Ativo);
        Assert.EndsWith($"/api/v1/fornecedores/{criado.Id}", resposta.Headers.Location?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Criar_grava_a_marca_de_comunidade_tradicional_no_banco()
    {
        using var admin = await api.ComoAdministradorAsync();
        var criado = await CriarAsync(admin, NovoRequest(comunidade: true));

        await using var conexao = new SqlConnection(api.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT ComunidadeTradicional FROM dbo.Fornecedor WHERE FornecedorId = @id";
        comando.Parameters.AddWithValue("@id", criado.Id);

        Assert.True((bool)(await comando.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Cnpj_duplicado_devolve_409()
    {
        using var admin = await api.ComoAdministradorAsync();
        var existente = await CriarAsync(admin);

        var resposta = await admin.PostAsJsonAsync("/api/v1/fornecedores", NovoRequest(existente.Cnpj));

        resposta.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("DOCUMENTO_DUPLICADO", await resposta.CodigoAsync());
    }

    [Theory]
    [InlineData("", "11222333000181")]
    [InlineData("Fornecedor", "12345678901")]
    [InlineData("Fornecedor", "")]
    public async Task Entrada_invalida_devolve_400(string razaoSocial, string cnpj)
    {
        using var admin = await api.ComoAdministradorAsync();

        var resposta = await admin.PostAsJsonAsync("/api/v1/fornecedores", new FornecedorRequest(razaoSocial, cnpj, null, null, false));

        resposta.AssertStatus(HttpStatusCode.BadRequest);
        Assert.Equal("VALIDACAO", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Obter_inexistente_devolve_404()
    {
        using var admin = await api.ComoAdministradorAsync();

        var resposta = await admin.GetAsync("/api/v1/fornecedores/987654");

        resposta.AssertStatus(HttpStatusCode.NotFound);
        Assert.Equal("FORNECEDOR_NAO_ENCONTRADO", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Atualizar_altera_os_dados_e_a_marca_de_comunidade()
    {
        using var almoxarife = await api.ComoAsync("Almoxarife");
        var criado = await CriarAsync(almoxarife);

        var resposta = await almoxarife.PutAsJsonAsync(
            $"/api/v1/fornecedores/{criado.Id}", new FornecedorRequest("Razão Nova", criado.Cnpj, null, null, true));

        resposta.AssertStatus(HttpStatusCode.OK);
        var atualizado = (await resposta.Content.ReadFromJsonAsync<FornecedorDto>())!;
        Assert.Equal("Razão Nova", atualizado.RazaoSocial);
        Assert.True(atualizado.ComunidadeTradicional);
    }

    [Fact]
    public async Task Listar_filtra_por_comunidade_tradicional_e_por_ativo()
    {
        using var admin = await api.ComoAdministradorAsync();
        var comunidade = await CriarAsync(admin, NovoRequest(comunidade: true));
        var comum = await CriarAsync(admin);

        var soComunidades = await admin.GetFromJsonAsync<Pagina<FornecedorDto>>("/api/v1/fornecedores?comunidadeTradicional=true&pageSize=100");
        (await admin.PatchAsJsonAsync($"/api/v1/fornecedores/{comum.Id}/ativo", new AlterarAtivoRequest(false)))
            .AssertStatus(HttpStatusCode.OK);
        var inativos = await admin.GetFromJsonAsync<Pagina<FornecedorDto>>($"/api/v1/fornecedores?busca={comum.Cnpj}&ativo=false");

        Assert.Contains(soComunidades!.Items, f => f.Id == comunidade.Id);
        Assert.DoesNotContain(soComunidades.Items, f => f.Id == comum.Id);
        Assert.All(soComunidades.Items, f => Assert.True(f.ComunidadeTradicional));
        Assert.Single(inativos!.Items);
    }
}
