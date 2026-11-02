using Aurora.Infrastructure.Erros;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

public static class ProcedureExtensions
{
    /// <summary>
    /// Executa a procedure e converte os erros dela (THROW 51001-51010) em regra de negócio já aqui, para que quem
    /// chama (como o lote offline) capture a falha de um item sem depender do middleware HTTP. O comando é sempre
    /// interpolado, então os valores seguem como parâmetros SQL.
    /// </summary>
    public static async Task ExecutarProcedureAsync(this AuroraDbContext db, FormattableString comando, CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync(comando, ct);
        }
        catch (SqlException ex) when (SqlErroTradutor.Traduzir(ex) is { } regra)
        {
            throw regra;
        }
    }
}
