using Aurora.Application.Comum;

namespace Aurora.Application.Tests;

public class PaginacaoTests
{
    [Theory]
    [InlineData(0, 0, 1, 1)]
    [InlineData(-5, -5, 1, 1)]
    [InlineData(3, 50, 3, 50)]
    [InlineData(1, 5000, 1, Paginacao.TamanhoMaximo)]
    [InlineData(int.MaxValue, 100, Paginacao.PaginaMaxima, 100)]
    public void Normaliza_pagina_e_tamanho_para_limites_seguros(int pagina, int tamanho, int paginaEsperada, int tamanhoEsperado)
    {
        var (p, t) = Paginacao.Normalizar(pagina, tamanho);

        Assert.Equal(paginaEsperada, p);
        Assert.Equal(tamanhoEsperado, t);
    }

    [Fact]
    public void O_deslocamento_maximo_cabe_num_int()
    {
        var (pagina, tamanho) = Paginacao.Normalizar(int.MaxValue, int.MaxValue);

        var deslocamento = checked((pagina - 1) * tamanho);

        Assert.True(deslocamento >= 0);
    }
}
