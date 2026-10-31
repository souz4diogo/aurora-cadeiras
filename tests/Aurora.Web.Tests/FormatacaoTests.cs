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

    [Theory]
    [InlineData(0, "0 min")]
    [InlineData(47, "47 min")]
    [InlineData(60, "1 h")]
    [InlineData(130, "2 h 10 min")]
    [InlineData(24 * 60, "1 d")]
    [InlineData(24 * 60 + 3 * 60, "1 d 3 h")]
    [InlineData(3 * 24 * 60 + 45, "3 d")]
    public void Duracao_usa_a_maior_unidade_que_cabe(int minutos, string esperado) =>
        Assert.Equal(esperado, Formatacao.Duracao(minutos));

    [Theory]
    [InlineData(10, "+10")]
    [InlineData(-2.5, "-2,5")]
    [InlineData(0, "0")]
    [InlineData(1500, "+1.500")]
    public void Quantidade_com_sinal_mostra_entrada_e_saida(double valor, string esperado) =>
        Assert.Equal(esperado, Formatacao.QuantidadeComSinal((decimal)valor));

    [Theory]
    [InlineData("EmAndamento", "Em andamento")]
    [InlineData("Concluida", "Concluída")]
    [InlineData("Planejada", "Planejada")]
    public void Situacao_da_ordem_e_da_etapa_sai_como_o_usuario_le(string status, string esperado)
    {
        Assert.Equal(esperado, Formatacao.StatusDaOrdem(status));
        Assert.Equal(esperado, Formatacao.StatusDaEtapa(status));
    }

    [Theory]
    [InlineData("Saida", "Saída")]
    [InlineData("Entrada", "Entrada")]
    public void Tipo_de_movimento_leva_acento(string tipo, string esperado) =>
        Assert.Equal(esperado, Formatacao.TipoDeMovimento(tipo));

    [Theory]
    [InlineData("Producao", "Produção")]
    [InlineData("ProducaoConcluida", "Produção concluída")]
    [InlineData("Expedicao", "Expedição")]
    [InlineData("Compra", "Compra")]
    public void Origem_do_movimento_leva_acento(string origem, string esperado) =>
        Assert.Equal(esperado, Formatacao.OrigemDoMovimento(origem));

    [Fact]
    public void Qr_code_e_um_svg_que_escala_sem_estilo_embutido_e_com_texto_alternativo()
    {
        var svg = CodigoQr.Svg("3f2b8c1e-5a4d-4e7b-9c10-2d6f8a1b7e33", "QR code da OP-2026-00001");

        Assert.StartsWith("<svg", svg, StringComparison.Ordinal);
        Assert.Contains("role=\"img\"", svg, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"QR code da OP-2026-00001\"", svg, StringComparison.Ordinal);
        Assert.Contains("viewBox=", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("style=", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Qr_code_de_codigos_diferentes_difere_e_o_mesmo_codigo_repete()
    {
        var a = CodigoQr.Svg("3f2b8c1e-5a4d-4e7b-9c10-2d6f8a1b7e33", "QR");
        var b = CodigoQr.Svg("9a1c7e20-8b3f-4d65-a2e4-5c0b9d3f1a88", "QR");

        Assert.NotEqual(a, b);
        Assert.Equal(a, CodigoQr.Svg("3f2b8c1e-5a4d-4e7b-9c10-2d6f8a1b7e33", "QR"));
    }

    [Fact]
    public void Texto_alternativo_do_qr_nao_deixa_sair_do_atributo()
    {
        var svg = CodigoQr.Svg("x", "\" onload=\"alert(1)");

        Assert.DoesNotContain("\" onload=", svg, StringComparison.Ordinal);
    }
}
