using System.Data.Common;
using Aurora.Application.Abstracoes;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Aurora.Infrastructure.Persistencia;

/// <summary>
/// Em cada conexão aberta define SESSION_CONTEXT('UsuarioId') para que triggers e procedures
/// saibam quem fez a ação (auditoria, apontamentos).
/// </summary>
public sealed class SessionContextInterceptor(ICurrentUser usuarioAtual) : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var comando = Criar(connection);
        comando?.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var comando = Criar(connection);
        if (comando is not null)
        {
            await comando.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private DbCommand? Criar(DbConnection connection)
    {
        if (usuarioAtual.UsuarioId is not int id)
        {
            return null;
        }

        var comando = connection.CreateCommand();
        comando.CommandText = "EXEC sp_set_session_context N'UsuarioId', @id;";
        var parametro = comando.CreateParameter();
        parametro.ParameterName = "@id";
        parametro.Value = id;
        comando.Parameters.Add(parametro);
        return comando;
    }
}
