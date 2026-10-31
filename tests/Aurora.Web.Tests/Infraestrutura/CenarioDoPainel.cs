using Aurora.Tests.Common;

namespace Aurora.Web.Tests.Infraestrutura;

/// <summary>Banco de teste + API + painel, criados uma vez e compartilhados pelos testes da coleção "painel".</summary>
public sealed class CenarioDoPainel : IAsyncLifetime
{
    private BancoDeTestes _banco = null!;

    public ApiHost Api { get; private set; } = null!;

    public PainelHost Painel { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _banco = await BancoDeTestes.CriarAsync();
        Api = new ApiHost(_banco.ConnectionString);
        Painel = new PainelHost(Api);
    }

    public async Task DisposeAsync()
    {
        await Painel.DisposeAsync();
        await Api.DisposeAsync();
        await _banco.DisposeAsync();
    }

    /// <summary>Navegador já autenticado como um usuário novo do perfil informado ("Administrador" usa o admin do seed).</summary>
    public async Task<Navegador> EntrarComoAsync(string perfil)
    {
        var (email, senha) = perfil == "Administrador"
            ? (ApiHost.EmailAdmin, ApiHost.SenhaAdmin)
            : await Api.CriarUsuarioAsync(perfil);
        var navegador = Painel.NovoNavegador();
        await navegador.EntrarAsync(email, senha);
        return navegador;
    }
}

[CollectionDefinition("painel")]
public sealed class PainelDefinicao : ICollectionFixture<CenarioDoPainel>;
