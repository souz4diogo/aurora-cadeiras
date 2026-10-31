using System.Net;
using System.Net.Http.Json;
using Aurora.Application.Clientes;
using Aurora.Application.Comum;
using Microsoft.Data.SqlClient;

namespace Aurora.Api.IntegrationTests;

[Collection("api")]
public class ClientesTests(ApiFactory api)
{
    private static string NovoCpf() => DadosDeTeste.Digitos(11);

    private static ClienteRequest NovoRequest(string? cpf = null) =>
        new("Cliente de Teste", cpf ?? NovoCpf(), "teste@cliente.local", "(11) 98888-7777", "Campinas", "SP");

    private static async Task<ClienteDto> CriarAsync(HttpClient cliente, ClienteRequest? request = null)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/v1/clientes", request ?? NovoRequest());
        resposta.AssertStatus(HttpStatusCode.Created);
        return (await resposta.Content.ReadFromJsonAsync<ClienteDto>())!;
    }

    [Fact]
    public async Task Criar_devolve_201_com_location_e_documento_somente_com_digitos()
    {
        using var vendedor = await api.ComoAsync("Vendedor");
        var cpf = NovoCpf();
        var mascarado = $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}";

        var resposta = await vendedor.PostAsJsonAsync("/api/v1/clientes", NovoRequest(mascarado));

        resposta.AssertStatus(HttpStatusCode.Created);
        var criado = (await resposta.Content.ReadFromJsonAsync<ClienteDto>())!;
        Assert.Equal(cpf, criado.CpfCnpj);
        Assert.Equal("SP", criado.Uf);
        Assert.True(criado.Ativo);
        Assert.EndsWith($"/api/v1/clientes/{criado.Id}", resposta.Headers.Location?.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Criar_grava_os_dados_no_banco()
    {
        using var admin = await api.ComoAdministradorAsync();
        var criado = await CriarAsync(admin);

        await using var conexao = new SqlConnection(api.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = "SELECT CpfCnpj, Uf, Email FROM dbo.Cliente WHERE ClienteId = @id";
        comando.Parameters.AddWithValue("@id", criado.Id);
        await using var leitor = await comando.ExecuteReaderAsync();

        Assert.True(await leitor.ReadAsync());
        Assert.Equal(criado.CpfCnpj, leitor.GetString(0));
        Assert.Equal("SP", leitor.GetString(1));
        Assert.Equal("teste@cliente.local", leitor.GetString(2));
    }

    [Fact]
    public async Task Documento_duplicado_devolve_409()
    {
        using var admin = await api.ComoAdministradorAsync();
        var existente = await CriarAsync(admin);

        var resposta = await admin.PostAsJsonAsync("/api/v1/clientes", NovoRequest(existente.CpfCnpj));

        resposta.AssertStatus(HttpStatusCode.Conflict);
        Assert.Equal("DOCUMENTO_DUPLICADO", await resposta.CodigoAsync());
    }

    [Theory]
    [InlineData("", "12345678901", null, "SP")]
    [InlineData("Nome", "123", null, "SP")]
    [InlineData("Nome", "12345678901", "sem-arroba", "SP")]
    [InlineData("Nome", "12345678901", null, "SAO")]
    public async Task Entrada_invalida_devolve_400(string nome, string documento, string? email, string? uf)
    {
        using var admin = await api.ComoAdministradorAsync();

        var resposta = await admin.PostAsJsonAsync("/api/v1/clientes", new ClienteRequest(nome, documento, email, null, null, uf));

        resposta.AssertStatus(HttpStatusCode.BadRequest);
        Assert.Equal("VALIDACAO", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Obter_inexistente_devolve_404()
    {
        using var admin = await api.ComoAdministradorAsync();

        var resposta = await admin.GetAsync("/api/v1/clientes/987654");

        resposta.AssertStatus(HttpStatusCode.NotFound);
        Assert.Equal("CLIENTE_NAO_ENCONTRADO", await resposta.CodigoAsync());
    }

    [Fact]
    public async Task Atualizar_altera_os_dados_e_permite_manter_o_proprio_documento()
    {
        using var vendedor = await api.ComoAsync("Vendedor");
        var criado = await CriarAsync(vendedor);

        var resposta = await vendedor.PutAsJsonAsync(
            $"/api/v1/clientes/{criado.Id}", new ClienteRequest("Nome Novo", criado.CpfCnpj, null, null, "Santos", "rj"));

        resposta.AssertStatus(HttpStatusCode.OK);
        var atualizado = (await resposta.Content.ReadFromJsonAsync<ClienteDto>())!;
        Assert.Equal("Nome Novo", atualizado.Nome);
        Assert.Equal("RJ", atualizado.Uf);
        Assert.Null(atualizado.Email);
    }

    [Fact]
    public async Task Atualizar_para_documento_de_outro_cliente_devolve_409()
    {
        using var admin = await api.ComoAdministradorAsync();
        var primeiro = await CriarAsync(admin);
        var segundo = await CriarAsync(admin);

        var resposta = await admin.PutAsJsonAsync($"/api/v1/clientes/{segundo.Id}", NovoRequest(primeiro.CpfCnpj));

        resposta.AssertStatus(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Inativar_altera_o_filtro_de_ativos()
    {
        using var admin = await api.ComoAdministradorAsync();
        var criado = await CriarAsync(admin);

        (await admin.PatchAsJsonAsync($"/api/v1/clientes/{criado.Id}/ativo", new AlterarAtivoRequest(false)))
            .AssertStatus(HttpStatusCode.OK);
        var ativos = await admin.GetFromJsonAsync<Pagina<ClienteDto>>($"/api/v1/clientes?busca={criado.CpfCnpj}&ativo=true");
        var inativos = await admin.GetFromJsonAsync<Pagina<ClienteDto>>($"/api/v1/clientes?busca={criado.CpfCnpj}&ativo=false");

        Assert.Empty(ativos!.Items);
        Assert.Single(inativos!.Items);
    }

    [Fact]
    public async Task Listar_pagina_e_busca_por_nome()
    {
        using var gerente = await api.ComoAsync("GerenteProducao");
        var nome = $"Busca{Guid.NewGuid():N}"[..14];
        using var admin = await api.ComoAdministradorAsync();
        await CriarAsync(admin, NovoRequest() with { Nome = nome });

        var encontrados = await gerente.GetFromJsonAsync<Pagina<ClienteDto>>($"/api/v1/clientes?busca={nome}");
        var pagina = await gerente.GetFromJsonAsync<Pagina<ClienteDto>>("/api/v1/clientes?pageSize=1");

        Assert.Single(encontrados!.Items);
        Assert.Single(pagina!.Items);
        Assert.Equal(1, pagina.PageSize);
        Assert.True(pagina.Total >= 1);
    }
}
