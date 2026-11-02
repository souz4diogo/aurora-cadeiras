using Aurora.Web.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aurora.Web.Tests;

public sealed class ProtecaoDeDadosTests : IDisposable
{
    private readonly string _pasta = Path.Combine(Path.GetTempPath(), "aurora-chaves-" + Guid.NewGuid().ToString("N"));

    private static IConfiguration Configuracao(string? caminho) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(caminho is null ? [] : new Dictionary<string, string?> { ["DataProtection:CaminhoDasChaves"] = caminho })
            .Build();

    private static ServiceProvider Montar(string? caminho)
    {
        var servicos = new ServiceCollection();
        servicos.AddLogging();
        servicos.AddAuroraProtecaoDeDados(Configuracao(caminho));
        return servicos.BuildServiceProvider();
    }

    [Fact]
    public void Duas_instancias_apontando_para_a_mesma_pasta_leem_o_que_a_outra_protegeu()
    {
        using var primeira = Montar(_pasta);
        using var segunda = Montar(_pasta);

        var protegido = primeira.GetRequiredService<IDataProtectionProvider>().CreateProtector("cookie").Protect("sessao-do-usuario");
        var aberto = segunda.GetRequiredService<IDataProtectionProvider>().CreateProtector("cookie").Unprotect(protegido);

        Assert.Equal("sessao-do-usuario", aberto);
        Assert.NotEmpty(Directory.GetFiles(_pasta, "key-*.xml"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Sem_pasta_configurada_o_comportamento_padrao_do_aspnet_continua(string? caminho)
    {
        var servicos = new ServiceCollection();

        servicos.AddAuroraProtecaoDeDados(Configuracao(caminho));

        Assert.Empty(servicos);
    }

    public void Dispose()
    {
        if (Directory.Exists(_pasta))
        {
            Directory.Delete(_pasta, recursive: true);
        }
    }
}
