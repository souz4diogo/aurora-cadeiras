using DbUp;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Aurora.Tests.Common;

/// <summary>
/// Banco SQL Server descartável para testes: sobe um container (Testcontainers), cria o banco e aplica
/// os scripts de <c>database/</c> com o DbUp. Defina AURORA_TEST_SQL com uma connection string para
/// usar um servidor existente no lugar do container (o banco aurora_test_* é apagado no fim).
/// </summary>
public sealed class BancoDeTestes : IAsyncDisposable
{
    private readonly MsSqlContainer? _container;
    private readonly string _servidor;
    private readonly string _nomeBanco;

    private BancoDeTestes(MsSqlContainer? container, string servidor, string nomeBanco, string connectionString)
    {
        _container = container;
        _servidor = servidor;
        _nomeBanco = nomeBanco;
        ConnectionString = connectionString;
    }

    /// <summary>Connection string do banco já migrado (sem pool, para o EXECUTE AS e o SESSION_CONTEXT não vazarem).</summary>
    public string ConnectionString { get; }

    public static async Task<BancoDeTestes> CriarAsync()
    {
        MsSqlContainer? container = null;
        var servidor = Environment.GetEnvironmentVariable("AURORA_TEST_SQL");
        if (string.IsNullOrWhiteSpace(servidor))
        {
            container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
            await container.StartAsync();
            servidor = container.GetConnectionString();
        }

        var nome = "aurora_test_" + Guid.NewGuid().ToString("N")[..8];
        var master = new SqlConnectionStringBuilder(servidor) { TrustServerCertificate = true, Pooling = false };
        await using (var conexao = new SqlConnection(master.ConnectionString))
        {
            await conexao.OpenAsync();
            await using var comando = conexao.CreateCommand();
            comando.CommandText = $"CREATE DATABASE [{nome}]";
            await comando.ExecuteNonQueryAsync();
        }

        var cs = new SqlConnectionStringBuilder(master.ConnectionString) { InitialCatalog = nome }.ConnectionString;
        var resultado = DeployChanges.To.SqlDatabase(cs)
            .WithScriptsFromFileSystem(LocalizarScripts())
            .LogToNowhere()
            .Build()
            .PerformUpgrade();

        if (!resultado.Successful)
        {
            throw new InvalidOperationException(
                $"Falha ao aplicar os scripts em {resultado.ErrorScript?.Name}: {resultado.Error?.Message}",
                resultado.Error);
        }

        return new BancoDeTestes(container, servidor, nome, cs);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        var master = new SqlConnectionStringBuilder(_servidor) { TrustServerCertificate = true, Pooling = false };
        await using var conexao = new SqlConnection(master.ConnectionString);
        await conexao.OpenAsync();
        await using var comando = conexao.CreateCommand();
        comando.CommandText =
            $"ALTER DATABASE [{_nomeBanco}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_nomeBanco}];";
        await comando.ExecuteNonQueryAsync();
    }

    private static string LocalizarScripts()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null)
        {
            var candidata = Path.Combine(pasta.FullName, "database");
            if (File.Exists(Path.Combine(candidata, "001-schema.sql")))
            {
                return candidata;
            }

            pasta = pasta.Parent;
        }

        throw new DirectoryNotFoundException("Pasta database/ com os scripts não encontrada.");
    }
}
