using Aurora.Domain.Enums;

namespace Aurora.Domain.Tests;

public class UnidadesDeMedidaTests
{
    [Theory]
    [InlineData(UnidadeDeMedida.Unidade, "un")]
    [InlineData(UnidadeDeMedida.Metro, "m")]
    [InlineData(UnidadeDeMedida.MetroQuadrado, "m2")]
    [InlineData(UnidadeDeMedida.MetroCubico, "m3")]
    [InlineData(UnidadeDeMedida.Litro, "l")]
    public void Converte_de_e_para_o_codigo_do_banco(UnidadeDeMedida unidade, string codigo)
    {
        Assert.Equal(codigo, unidade.ParaCodigo());
        Assert.Equal(unidade, UnidadesDeMedida.DeCodigo(codigo));
    }

    [Theory]
    [InlineData("kg")]
    [InlineData("M3")]
    [InlineData("")]
    [InlineData(null)]
    public void Codigo_desconhecido_nao_e_aceito(string? codigo)
    {
        Assert.False(UnidadesDeMedida.TentarObter(codigo, out _));
        Assert.Throws<ArgumentOutOfRangeException>(() => UnidadesDeMedida.DeCodigo(codigo!));
    }

    [Fact]
    public void Todas_as_unidades_tem_codigo()
    {
        Assert.All(Enum.GetValues<UnidadeDeMedida>(), u => Assert.False(string.IsNullOrEmpty(u.ParaCodigo())));
    }
}
