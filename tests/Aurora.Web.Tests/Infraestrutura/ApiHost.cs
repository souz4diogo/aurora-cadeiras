extern alias api;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using Aurora.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Aurora.Web.Tests.Infraestrutura;

/// <summary>API real em memória, ligada a um SQL Server de teste já migrado: o painel conversa com ela como em produção.</summary>
public sealed class ApiHost(string connectionString) : WebApplicationFactory<api::Program>
{
    public const string EmailAdmin = "admin@teste.local";
    public const string SenhaAdmin = "Senha-Admin-Teste-1";
    private const string ChaveJwt = "chave-de-teste-com-mais-de-32-caracteres-0123456789";

    /// <summary>Ids da tabela Perfil (seed), na ordem em que foram criados.</summary>
    private static readonly Dictionary<string, byte> IdsDosPerfis = new()
    {
        ["Administrador"] = 1,
        ["GerenteProducao"] = 2,
        ["Operador"] = 3,
        ["Vendedor"] = 4,
        ["Almoxarife"] = 5,
        ["Inspetor"] = 6,
    };

    public string ConnectionString { get; } = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration((_, configuracao) => configuracao.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Aurora"] = ConnectionString,
            ["Jwt:Key"] = ChaveJwt,
            ["Seed:AdminEmail"] = EmailAdmin,
            ["Seed:AdminPassword"] = SenhaAdmin,
            ["RateLimit:LoginPorMinuto"] = "1000",
        }));

    /// <summary>Cria um usuário do perfil informado pelo administrador e devolve as credenciais para entrar pelo painel.</summary>
    public async Task<(string Email, string Senha)> CriarUsuarioAsync(string perfil)
    {
        using var admin = CreateClient();
        var login = await admin.PostAsJsonAsync("/api/v1/auth/login", new { Email = EmailAdmin, Senha = SenhaAdmin });
        login.EnsureSuccessStatusCode();
        var token = (await login.Content.ReadFromJsonAsync<TokenDeTeste>())!;
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        var perfilId = IdsDosPerfis[perfil];
        var email = $"{perfil.ToLowerInvariant()}.{Guid.NewGuid():N}@teste.local";
        var criado = await admin.PostAsJsonAsync("/api/v1/usuarios", new { Nome = $"Usuário {perfil}", Email = email, PerfilId = perfilId });
        criado.EnsureSuccessStatusCode();
        var resposta = (await criado.Content.ReadFromJsonAsync<UsuarioCriadoDeTeste>())!;
        return (email, resposta.SenhaProvisoria);
    }

    private sealed record TokenDeTeste(string AccessToken);

    private sealed record UsuarioCriadoDeTeste(string SenhaProvisoria);
}
