using System.Data;
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
    /// <remarks>
    /// Não use para <see cref="decimal"/>: o parâmetro interpolado chega ao SQL Server com 2 casas e arredonda
    /// quantidades como 4,125 para 4,13. Para decimais, use a sobrecarga com <see cref="Parametro.Numerica"/>.
    /// </remarks>
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

    /// <summary>
    /// Executa a procedure com parâmetros tipados (precisão e escala explícitas). O texto do comando é sempre uma
    /// constante do código, com os valores entrando só por <paramref name="parametros"/>, nunca concatenados.
    /// </summary>
    public static async Task ExecutarProcedureAsync(
        this AuroraDbContext db, string comando, IReadOnlyList<SqlParameter> parametros, CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(comando, parametros.Cast<object>().ToArray(), ct);
        }
        catch (SqlException ex) when (SqlErroTradutor.Traduzir(ex) is { } regra)
        {
            throw regra;
        }
    }
}

/// <summary>Fábrica de parâmetros SQL com o tipo exato da coluna ou do parâmetro da procedure.</summary>
public static class Parametro
{
    public static SqlParameter Inteiro(string nome, int valor) => new(nome, SqlDbType.Int) { Value = valor };

    /// <summary>Decimal com precisão e escala da coluna (ex.: 12 e 3 para quantidades): sem isso o valor é arredondado.</summary>
    public static SqlParameter Numerica(string nome, decimal valor, byte precisao, byte escala) =>
        new(nome, SqlDbType.Decimal) { Precision = precisao, Scale = escala, Value = valor };

    public static SqlParameter Texto(string nome, string valor, int tamanho) =>
        new(nome, SqlDbType.NVarChar, tamanho) { Value = valor };
}
