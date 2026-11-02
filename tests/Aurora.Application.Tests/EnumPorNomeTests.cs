using Aurora.Application.Comum;
using Aurora.Domain.Enums;

namespace Aurora.Application.Tests;

public class EnumPorNomeTests
{
    [Theory]
    [InlineData("Refugo", TipoPerda.Refugo)]
    [InlineData("retrabalho", TipoPerda.Retrabalho)]
    [InlineData("  RETRABALHO  ", TipoPerda.Retrabalho)]
    public void Aceita_o_nome_sem_diferenciar_maiusculas_nem_espacos_nas_pontas(string texto, TipoPerda esperado)
    {
        Assert.True(EnumPorNome.TentarObter<TipoPerda>(texto, out var valor));
        Assert.Equal(esperado, valor);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Perda")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("99")]
    [InlineData("Refugo,Retrabalho")]
    public void Recusa_vazio_nome_desconhecido_e_numeros(string? texto)
    {
        Assert.False(EnumPorNome.TentarObter<TipoPerda>(texto, out var valor));
        Assert.Equal(default, valor);
    }

    [Fact]
    public void Funciona_para_qualquer_enum()
    {
        Assert.True(EnumPorNome.TentarObter<ResultadoInspecao>("reprovada", out var resultado));
        Assert.Equal(ResultadoInspecao.Reprovada, resultado);
        Assert.True(EnumPorNome.TentarObter<TipoApontamento>("Fim", out var tipo));
        Assert.Equal(TipoApontamento.Fim, tipo);
    }
}
