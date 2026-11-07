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

    [Fact]
    public void O_dia_de_negocio_comeca_as_3h_UTC_e_um_movimento_as_22h_de_Brasilia_cai_no_proprio_dia()
    {
        var dia = new DateOnly(2026, 11, 3);
        var as22hDeBrasilia = new DateTime(2026, 11, 4, 1, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 11, 3, 3, 0, 0, DateTimeKind.Utc), DiaDeNegocio.InicioEmUtc(dia));
        Assert.Equal(DateTimeKind.Utc, DiaDeNegocio.InicioEmUtc(dia).Kind);
        Assert.InRange(as22hDeBrasilia, DiaDeNegocio.InicioEmUtc(dia), DiaDeNegocio.InicioEmUtc(dia.AddDays(1)).AddTicks(-1));
    }
}
