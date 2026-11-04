using System.Globalization;
using Aurora.Application.Comum;

namespace Aurora.Application.Tests;

public class DiaDeNegocioTests
{
    [Theory]
    [InlineData("2026-11-03T02:59:59", "2026-11-02")] // 23h59 em Brasília ainda é o dia anterior
    [InlineData("2026-11-03T03:00:00", "2026-11-03")] // meia-noite em Brasília
    [InlineData("2026-11-03T23:59:59", "2026-11-03")]
    [InlineData("2026-11-03T00:00:00", "2026-11-02")] // meia-noite UTC ainda é a noite anterior
    [InlineData("2026-11-04T01:00:00", "2026-11-03")] // 22h em Brasília: o prazo de hoje ainda não venceu
    [InlineData("2026-01-01T01:00:00", "2025-12-31")] // virada de ano
    public void O_dia_de_negocio_e_o_de_Brasilia(string utc, string esperado)
    {
        var instante = DateTime.SpecifyKind(DateTime.Parse(utc, CultureInfo.InvariantCulture), DateTimeKind.Utc);

        Assert.Equal(DateOnly.Parse(esperado, CultureInfo.InvariantCulture), DiaDeNegocio.De(instante));
    }
}
