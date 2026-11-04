using System.Globalization;
using Aurora.Application.Relatorios;

namespace Aurora.Infrastructure.Relatorios;

/// <summary>Como uma célula tipada vira texto em pt-BR nos relatórios (no PDF) e como as datas são ajustadas ao horário local.</summary>
internal static class FormatadorDeCelulas
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Horário de Brasília (UTC-3). O Brasil não tem horário de verão desde 2019, então um deslocamento fixo dispensa o
    /// banco de fusos horários, que a imagem Alpine do contêiner não traz.
    /// </summary>
    public static readonly TimeSpan DeslocamentoDeBrasilia = Aurora.Application.Comum.DiaDeNegocio.Deslocamento;

    public static DateTime ParaHorarioLocal(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(DeslocamentoDeBrasilia);

    public static string Texto(object? valor, TipoDeColuna tipo) => valor switch
    {
        null => "",
        bool b => b ? "Sim" : "Não",
        DateOnly d => d.ToString("dd/MM/yyyy", PtBr),
        DateTime dt when tipo == TipoDeColuna.Data => dt.ToString("dd/MM/yyyy", PtBr),
        DateTime dt => ParaHorarioLocal(dt).ToString("dd/MM/yyyy HH:mm", PtBr),
        decimal m when tipo == TipoDeColuna.Moeda => m.ToString("C2", PtBr),
        decimal p when tipo == TipoDeColuna.Percentual => p.ToString("N2", PtBr) + "%",
        decimal q when tipo == TipoDeColuna.Quantidade => q.ToString("#,##0.###", PtBr),
        IFormattable numero when tipo == TipoDeColuna.Inteiro => numero.ToString("N0", PtBr),
        IFormattable numero => numero.ToString(null, PtBr),
        _ => valor.ToString() ?? "",
    };

    public static bool AlinhaADireita(TipoDeColuna tipo) =>
        tipo is TipoDeColuna.Inteiro or TipoDeColuna.Quantidade or TipoDeColuna.Moeda or TipoDeColuna.Percentual;

    /// <summary>Quando foi gerado, em horário de Brasília.</summary>
    public static string GeradoEm(DateTime utc) => $"Gerado em {ParaHorarioLocal(utc):dd/MM/yyyy HH:mm} (horário de Brasília)";
}
