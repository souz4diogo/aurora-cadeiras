using Aurora.Api.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

namespace Aurora.Api.IntegrationTests;

public class TelemetriaTests
{
    private static IConfiguration Configuracao(string? conexao) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(conexao is null ? [] : new Dictionary<string, string?> { ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = conexao })
            .Build();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sem_connection_string_nada_e_registrado(string? conexao)
    {
        var servicos = new ServiceCollection();

        servicos.AddAuroraTelemetria(Configuracao(conexao));

        Assert.Empty(servicos);
    }

    [Fact]
    public void Com_connection_string_registra_o_provedor_de_rastros()
    {
        var servicos = new ServiceCollection();

        servicos.AddAuroraTelemetria(
            Configuracao("InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://exemplo.invalid/"));

        Assert.Contains(servicos, d => d.ServiceType == typeof(TracerProvider));
    }
}
