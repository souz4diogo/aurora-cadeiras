using Aurora.Web.Extensions;

namespace Aurora.Web.Tests;

public class FormatacaoTests
{
    [Fact]
    public void Cultura_pt_br_esta_disponivel_de_verdade_neste_ambiente()
    {
        Formatacao.GarantirCulturaPtBr();

        Assert.Equal("1.234,5", 1234.5m.ToString("N1", Formatacao.PtBr));
    }

    [Theory]
    [InlineData(1290, "1.290,00")]
    [InlineData(0.5, "0,50")]
    [InlineData(1234567.891, "1.234.567,89")]
    public void Moeda_usa_ponto_nos_milhares_e_virgula_nos_centavos(double valor, string esperado)
    {
        var texto = Formatacao.Moeda((decimal)valor);

        Assert.StartsWith("R$", texto, StringComparison.Ordinal);
        Assert.EndsWith(esperado, texto, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2.5, "2,5")]
    [InlineData(10, "10")]
    [InlineData(0.025, "0,025")]
    [InlineData(1500, "1.500")]
    public void Quantidade_tira_zeros_a_direita(double valor, string esperado) =>
        Assert.Equal(esperado, Formatacao.Quantidade((decimal)valor));

    [Fact]
    public void Datas_saem_no_padrao_brasileiro() =>
        Assert.Equal("30/10/2026", Formatacao.Data(new DateOnly(2026, 10, 30)));

    [Theory]
    [InlineData("12345678901", "123.456.789-01")]
    [InlineData("12345678000195", "12.345.678/0001-95")]
    [InlineData("123", "123")]
    [InlineData(null, "")]
    public void Documento_e_formatado_como_cpf_ou_cnpj_conforme_o_tamanho(string? digitos, string esperado) =>
        Assert.Equal(esperado, Formatacao.Documento(digitos));

    [Theory]
    [InlineData("EmProducao", "Em produção")]
    [InlineData("Aberto", "Aberto")]
    public void Status_do_pedido_e_escrito_como_o_usuario_le(string status, string esperado) =>
        Assert.Equal(esperado, Formatacao.StatusDoPedido(status));

    [Fact]
    public void Zeros_a_mais_do_banco_somem_do_campo_do_formulario() =>
        Assert.Equal("2,5", Formatacao.SemZerosAMais(2.500m).ToString(Formatacao.PtBr));
}
