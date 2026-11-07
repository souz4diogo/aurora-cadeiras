namespace Aurora.Application.Comum;

/// <summary>
/// O "dia" do negócio é o de Brasília (UTC-3). O servidor e o banco trabalham em UTC; sem esta conversão, entre 21h e
/// 24h tudo o que vence hoje apareceria como atrasado. O Brasil não tem horário de verão desde 2019, então um
/// deslocamento fixo dispensa o banco de fusos horários (a imagem Alpine do contêiner não o traz).
/// O mesmo deslocamento vale no SQL (script 008) e nos relatórios.
/// </summary>
public static class DiaDeNegocio
{
    public static readonly TimeSpan Deslocamento = TimeSpan.FromHours(-3);

    /// <summary>A data de negócio em que cai um instante em UTC.</summary>
    public static DateOnly De(DateTime utc) => DateOnly.FromDateTime(utc.Add(Deslocamento));

    /// <summary>
    /// O instante em UTC em que começa um dia de negócio (meia-noite de Brasília = 03:00 UTC). Serve para filtrar
    /// horários gravados em UTC por um período em dias de Brasília: [InicioEmUtc(de), InicioEmUtc(ate + 1)).
    /// </summary>
    public static DateTime InicioEmUtc(DateOnly dia) => dia.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) - Deslocamento;
}
