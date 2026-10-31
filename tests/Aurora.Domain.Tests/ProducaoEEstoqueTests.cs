using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;

namespace Aurora.Domain.Tests;

public class ProducaoEEstoqueTests
{
    private static T Instanciar<T>(params (string Propriedade, object Valor)[] valores)
        where T : class
    {
        var entidade = (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;
        foreach (var (propriedade, valor) in valores)
        {
            typeof(T).GetProperty(propriedade)!.SetValue(entidade, valor);
        }

        return entidade;
    }

    [Fact]
    public void Perda_guarda_os_dados_informados()
    {
        var perda = new RegistroPerda(341, TipoPerda.Retrabalho, 3, "Acabamento irregular", usuarioId: 9);

        Assert.Equal(341, perda.OrdemProducaoEtapaId);
        Assert.Equal(TipoPerda.Retrabalho, perda.Tipo);
        Assert.Equal(3, perda.Quantidade);
        Assert.Equal("Acabamento irregular", perda.Motivo);
        Assert.Equal(9, perda.UsuarioId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Perda_com_quantidade_nao_positiva_e_recusada(int quantidade)
    {
        var erro = Assert.Throws<RegraDeNegocioException>(
            () => new RegistroPerda(341, TipoPerda.Refugo, quantidade, "Motivo", 9));

        Assert.Equal("QUANTIDADE_INVALIDA", erro.Codigo);
        Assert.Equal(TipoErro.EntidadeNaoProcessavel, erro.Tipo);
    }

    [Fact]
    public void Alerta_novo_esta_aberto_e_ao_ser_lido_guarda_quem_e_quando()
    {
        var alerta = Instanciar<AlertaEstoque>();
        var momento = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

        Assert.False(alerta.Lido);
        alerta.MarcarComoLido(usuarioId: 4, momento);

        Assert.True(alerta.Lido);
        Assert.Equal(4, alerta.LidoPorId);
        Assert.Equal(momento, alerta.LidoEm);
    }

    [Fact]
    public void Alerta_ja_lido_nao_troca_quem_leu_nem_quando()
    {
        var alerta = Instanciar<AlertaEstoque>();
        var primeira = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        alerta.MarcarComoLido(4, primeira);

        alerta.MarcarComoLido(8, primeira.AddHours(2));

        Assert.Equal(4, alerta.LidoPorId);
        Assert.Equal(primeira, alerta.LidoEm);
    }

    [Theory]
    [InlineData(10, 4, 6)]
    [InlineData(5, 5, 0)]
    [InlineData(0, 0, 0)]
    public void Disponivel_e_o_saldo_menos_o_reservado(int quantidade, int reservado, int esperado)
    {
        var estoque = Instanciar<EstoqueProduto>(
            (nameof(EstoqueProduto.Quantidade), quantidade), (nameof(EstoqueProduto.Reservado), reservado));

        Assert.Equal(esperado, estoque.Disponivel);
    }
}
