using System.Net;
using Aurora.Domain.Excecoes;
using Aurora.Infrastructure.Erros;
using Microsoft.Data.SqlClient;

namespace Aurora.Api.IntegrationTests;

[Collection("api")]
public class InfraestruturaTests(ApiFactory api)
{
    [Fact]
    public async Task Health_responde_sem_autenticacao()
    {
        using var cliente = api.CreateClient();

        var vivo = await cliente.GetAsync("/health");
        var pronto = await cliente.GetAsync("/health/ready");

        vivo.AssertStatus(HttpStatusCode.OK);
        pronto.AssertStatus(HttpStatusCode.OK);
        Assert.Equal("Healthy", await pronto.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Swagger_documenta_o_login_e_o_esquema_bearer()
    {
        using var cliente = api.CreateClient();

        var resposta = await cliente.GetAsync("/swagger/v1/swagger.json");

        resposta.AssertStatus(HttpStatusCode.OK);
        var json = await resposta.Content.ReadAsStringAsync();
        Assert.Contains("/api/v1/auth/login", json, StringComparison.Ordinal);
        Assert.Contains("\"bearer\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Respostas_trazem_cabecalhos_de_seguranca_e_nao_expoem_o_servidor()
    {
        using var cliente = api.CreateClient();

        var resposta = await cliente.GetAsync("/health");

        Assert.Equal("nosniff", resposta.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", resposta.Headers.GetValues("X-Frame-Options").Single());
        Assert.False(resposta.Headers.Contains("Server"));
    }

    [Fact]
    public async Task Rota_inexistente_devolve_404_em_problem_json()
    {
        using var cliente = await api.ComoAdministradorAsync();

        var resposta = await cliente.GetAsync("/api/v1/nao-existe");

        resposta.AssertStatus(HttpStatusCode.NotFound);
        Assert.Equal("NAO_ENCONTRADO", await resposta.CodigoAsync());
    }

    [Theory]
    [InlineData(51001, "RN04", TipoErro.Conflito)]
    [InlineData(51002, "RN05", TipoErro.Conflito)]
    [InlineData(51003, "RN01", TipoErro.Conflito)]
    [InlineData(51004, "RN07", TipoErro.Conflito)]
    [InlineData(51005, "STATUS_INVALIDO", TipoErro.Conflito)]
    [InlineData(51006, "RN06", TipoErro.EntidadeNaoProcessavel)]
    [InlineData(51007, "RECEBIMENTO_EXCEDENTE", TipoErro.EntidadeNaoProcessavel)]
    [InlineData(51008, "NAO_ENCONTRADO", TipoErro.NaoEncontrado)]
    [InlineData(51009, "MOVIMENTO_IMUTAVEL", TipoErro.Conflito)]
    [InlineData(51010, "PARAMETRO_INVALIDO", TipoErro.EntidadeNaoProcessavel)]
    public async Task Erros_das_procedures_sao_traduzidos_para_regras_de_negocio(int numero, string codigo, TipoErro tipo)
    {
        await using var conexao = new SqlConnection(api.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText = $"THROW {numero}, N'Mensagem da procedure.', 1;";

        var erro = await Assert.ThrowsAsync<SqlException>(() => comando.ExecuteNonQueryAsync());
        var regra = SqlErroTradutor.Traduzir(erro);

        Assert.NotNull(regra);
        Assert.Equal(codigo, regra.Codigo);
        Assert.Equal(tipo, regra.Tipo);
        Assert.Equal("Mensagem da procedure.", regra.Message);
    }

    [Fact]
    public void Erro_sql_desconhecido_nao_e_traduzido()
    {
        var erro = Assert.Throws<SqlException>(() =>
        {
            using var conexao = new SqlConnection(api.ConnectionString);
            conexao.Open();
            using var comando = conexao.CreateCommand();
            comando.CommandText = "SELECT 1/0;";
            comando.ExecuteNonQuery();
        });

        Assert.Null(SqlErroTradutor.Traduzir(erro));
    }
}
