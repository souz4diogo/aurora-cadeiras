using Aurora.Tests.Common;

namespace Aurora.Database.Tests;

/// <summary>Banco migrado (ver <see cref="BancoDeTestes"/>) e os usuários usados pelos testes.</summary>
public sealed class BancoFixture : IAsyncLifetime
{
    private BancoDeTestes _banco = null!;

    public Db Db { get; private set; } = null!;

    public Usuarios Usuarios { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _banco = await BancoDeTestes.CriarAsync();
        Db = new Db(_banco.ConnectionString);
        Usuarios = await CriarUsuariosAsync(Db);
    }

    public async Task DisposeAsync() => await _banco.DisposeAsync();

    private static async Task<Usuarios> CriarUsuariosAsync(Db db)
    {
        async Task<int> NovoAsync(string nome, string perfil) =>
            await db.InteiroAsync(
                "INSERT dbo.Usuario (Nome, Email, SenhaHash, PerfilId) " +
                "SELECT @nome, @email, 'hash-de-teste', PerfilId FROM dbo.Perfil WHERE Nome = @perfil; " +
                "SELECT CAST(SCOPE_IDENTITY() AS int);",
                null,
                ("@nome", nome),
                ("@email", $"{nome.ToLowerInvariant()}@teste.local"),
                ("@perfil", perfil));

        return new Usuarios(
            Vendedor: await NovoAsync("Vendedor", "Vendedor"),
            Operador: await NovoAsync("Operador", "Operador"),
            Inspetor: await NovoAsync("Inspetor", "Inspetor"),
            Almoxarife: await NovoAsync("Almoxarife", "Almoxarife"));
    }
}

public sealed record Usuarios(int Vendedor, int Operador, int Inspetor, int Almoxarife);

[CollectionDefinition("banco")]
public sealed class BancoDefinicao : ICollectionFixture<BancoFixture>;
