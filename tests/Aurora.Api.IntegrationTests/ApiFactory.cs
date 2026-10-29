using System.Net.Http.Headers;
using System.Net.Http.Json;
using Aurora.Application.Autenticacao;
using Aurora.Application.Usuarios;
using Aurora.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Aurora.Api.IntegrationTests;

/// <summary>API em memória apontando para um SQL Server real (container) já migrado.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string EmailAdmin = "admin@teste.local";
    public const string SenhaAdmin = "Senha-Admin-Teste-1";
    public const string ChaveJwt = "chave-de-teste-com-mais-de-32-caracteres-0123456789";

    private readonly SemaphoreSlim _trava = new(1, 1);
    private readonly Dictionary<string, (string Email, string Senha)> _usuarios = [];
    private BancoDeTestes _banco = null!;

    public string ConnectionString => _banco.ConnectionString;

    public async Task InitializeAsync() => _banco = await BancoDeTestes.CriarAsync();

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _banco.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration((_, configuracao) => configuracao.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Aurora"] = _banco.ConnectionString,
            ["Jwt:Key"] = ChaveJwt,
            ["Seed:AdminEmail"] = EmailAdmin,
            ["Seed:AdminPassword"] = SenhaAdmin,
            ["RateLimit:LoginPorMinuto"] = "1000",
            ["Swagger:Habilitado"] = "true",
        }));

    public async Task<TokenResponse> LoginAsync(string email, string senha)
    {
        using var cliente = CreateClient();
        var resposta = await cliente.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, senha));
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<TokenResponse>())!;
    }

    public async Task<HttpClient> ComoAdministradorAsync() => await ComoAsync("Administrador");

    /// <summary>Cliente HTTP autenticado como um usuário do perfil informado (criado uma vez pelo administrador).</summary>
    public async Task<HttpClient> ComoAsync(string perfil)
    {
        var (email, senha) = await GarantirUsuarioAsync(perfil);
        var token = await LoginAsync(email, senha);
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return cliente;
    }

    public async Task<(UsuarioDto Usuario, string Senha)> CriarUsuarioAsync(
        string perfil = "Vendedor", string? email = null)
    {
        using var admin = await ComoAdministradorAsync();
        var perfilId = (byte)Enum.Parse<Aurora.Domain.Enums.PerfilUsuario>(perfil);
        int? clienteId = perfil == "Cliente" ? 1 : null;
        email ??= $"{perfil.ToLowerInvariant()}.{Guid.NewGuid():N}@teste.local";

        var resposta = await admin.PostAsJsonAsync("/api/v1/usuarios", new CriarUsuarioRequest($"Usuário {perfil}", email, perfilId, clienteId));
        resposta.EnsureSuccessStatusCode();
        var criado = (await resposta.Content.ReadFromJsonAsync<UsuarioComSenhaProvisoriaDto>())!;
        return (criado.Usuario, criado.SenhaProvisoria);
    }

    private async Task<(string Email, string Senha)> GarantirUsuarioAsync(string perfil)
    {
        if (perfil == "Administrador")
        {
            return (EmailAdmin, SenhaAdmin);
        }

        await _trava.WaitAsync();
        try
        {
            if (!_usuarios.TryGetValue(perfil, out var credenciais))
            {
                var (usuario, senha) = await CriarUsuarioAsync(perfil);
                credenciais = (usuario.Email, senha);
                _usuarios[perfil] = credenciais;
            }

            return credenciais;
        }
        finally
        {
            _trava.Release();
        }
    }
}

[CollectionDefinition("api")]
public sealed class ApiDefinicao : ICollectionFixture<ApiFactory>;
