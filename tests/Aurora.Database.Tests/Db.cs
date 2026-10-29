using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Aurora.Database.Tests;

/// <summary>Acesso ADO.NET mínimo para os testes; define o UsuarioId no SESSION_CONTEXT como a API faz.</summary>
public sealed class Db(string connectionString, string? usuarioDoBanco = null)
{
    /// <summary>Devolve um acesso que executa tudo como o usuário de banco informado (EXECUTE AS USER).</summary>
    public Db Como(string usuario) => new(connectionString, usuario);

    public async Task ExecutarAsync(string sql, int? usuarioId = null, params (string Nome, object? Valor)[] parametros)
    {
        await using var conexao = await AbrirAsync(usuarioId);
        await using var comando = Criar(conexao, sql, parametros);
        await comando.ExecuteNonQueryAsync();
    }

    public async Task<object?> EscalarAsync(string sql, int? usuarioId = null, params (string Nome, object? Valor)[] parametros)
    {
        await using var conexao = await AbrirAsync(usuarioId);
        await using var comando = Criar(conexao, sql, parametros);
        var valor = await comando.ExecuteScalarAsync();
        return valor is DBNull ? null : valor;
    }

    public async Task<int> InteiroAsync(string sql, int? usuarioId = null, params (string Nome, object? Valor)[] parametros) =>
        Convert.ToInt32(await EscalarAsync(sql, usuarioId, parametros), CultureInfo.InvariantCulture);

    public async Task<decimal> DecimalAsync(string sql, int? usuarioId = null, params (string Nome, object? Valor)[] parametros) =>
        Convert.ToDecimal(await EscalarAsync(sql, usuarioId, parametros), CultureInfo.InvariantCulture);

    public async Task<string?> TextoAsync(string sql, int? usuarioId = null, params (string Nome, object? Valor)[] parametros) =>
        Convert.ToString(await EscalarAsync(sql, usuarioId, parametros), CultureInfo.InvariantCulture);

    public async Task<List<int>> ListaAsync(string sql, int? usuarioId = null, params (string Nome, object? Valor)[] parametros)
    {
        await using var conexao = await AbrirAsync(usuarioId);
        await using var comando = Criar(conexao, sql, parametros);
        await using var leitor = await comando.ExecuteReaderAsync();
        var lista = new List<int>();
        while (await leitor.ReadAsync())
        {
            lista.Add(leitor.GetInt32(0));
        }

        return lista;
    }

    /// <summary>Executa o comando esperando uma falha e devolve o número do erro SQL (ex.: 51001).</summary>
    public async Task<int> ErroAsync(string sql, int? usuarioId = null, params (string Nome, object? Valor)[] parametros)
    {
        var excecao = await Assert.ThrowsAnyAsync<SqlException>(() => ExecutarAsync(sql, usuarioId, parametros));
        return excecao.Number;
    }

    private async Task<SqlConnection> AbrirAsync(int? usuarioId)
    {
        var conexao = new SqlConnection(connectionString);
        await conexao.OpenAsync();

        if (usuarioId is not null)
        {
            await using var contexto = conexao.CreateCommand();
            contexto.CommandText = "EXEC sp_set_session_context N'UsuarioId', @id";
            contexto.Parameters.AddWithValue("@id", usuarioId.Value);
            await contexto.ExecuteNonQueryAsync();
        }

        if (usuarioDoBanco is not null)
        {
            await using var personificar = conexao.CreateCommand();
            personificar.CommandText = $"EXECUTE AS USER = '{usuarioDoBanco}'";
            await personificar.ExecuteNonQueryAsync();
        }

        return conexao;
    }

    private static SqlCommand Criar(SqlConnection conexao, string sql, (string Nome, object? Valor)[] parametros)
    {
        var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        foreach (var (nome, valor) in parametros)
        {
            comando.Parameters.AddWithValue(nome, valor ?? DBNull.Value);
        }

        return comando;
    }
}
