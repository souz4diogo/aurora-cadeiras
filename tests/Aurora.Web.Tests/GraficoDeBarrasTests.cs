using Aurora.Web.Extensions;

namespace Aurora.Web.Tests;

public class GraficoDeBarrasTests
{
    private static string Gerar(params GraficoDeBarras.Barra[] barras) => GraficoDeBarras.Svg("Titulo", "Descricao", barras);

    [Fact]
    public void Maior_valor_ocupa_a_largura_toda_e_os_demais_sao_proporcionais()
    {
        var svg = Gerar(new("A", 10, "10"), new("B", 5, "5"));

        Assert.Contains("width=\"360\"", svg, StringComparison.Ordinal);
        Assert.Contains("width=\"180\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Texto_vindo_dos_dados_e_codificado_e_nao_ha_script_nem_estilo_embutido()
    {
        var svg = GraficoDeBarras.Svg("<b>t</b>", "d\"q", [new("<script>alert(1)</script>", 1, "<1>")]);

        Assert.DoesNotContain("<script", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<b>", svg, StringComparison.Ordinal);
        Assert.DoesNotContain("style=", svg, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Zero_nao_desenha_barra_e_valor_minusculo_continua_visivel()
    {
        var svg = Gerar(new("Zero", 0, "0"), new("Pouco", 0.001, "0"), new("Muito", 1000, "1000"));

        Assert.Contains("width=\"0\"", svg, StringComparison.Ordinal);
        Assert.Contains("width=\"2\"", svg, StringComparison.Ordinal);
    }

    [Fact]
    public void Sem_dados_ou_so_com_zeros_gera_um_svg_valido_com_titulo_e_descricao()
    {
        var vazio = Gerar();
        var zeros = Gerar(new GraficoDeBarras.Barra("A", 0, "0"));

        foreach (var svg in new[] { vazio, zeros })
        {
            Assert.StartsWith("<svg", svg, StringComparison.Ordinal);
            Assert.Contains("role=\"img\"", svg, StringComparison.Ordinal);
            Assert.Contains("<title id=\"grafico-titulo\">Titulo</title>", svg, StringComparison.Ordinal);
            Assert.Contains("<desc id=\"grafico-descricao\">Descricao</desc>", svg, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Rotulo_comprido_e_abreviado_com_reticencias()
    {
        var svg = Gerar(new GraficoDeBarras.Barra(new string('x', 60), 1, "1"));

        Assert.DoesNotContain(new string('x', 30), svg, StringComparison.Ordinal);
        Assert.Contains("…", svg, StringComparison.Ordinal);
    }
}
