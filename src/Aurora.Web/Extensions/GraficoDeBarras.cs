using System.Globalization;
using System.Net;
using System.Text;

namespace Aurora.Web.Extensions;

/// <summary>
/// Gráfico de barras horizontais em SVG gerado no servidor. A política de conteúdo do painel não aceita script nem
/// estilo embutido, então o desenho usa só atributos e classes do <c>site.css</c>. O gráfico é um complemento visual:
/// quem usa leitor de tela encontra os mesmos números na tabela ao lado (o SVG traz título e descrição).
/// </summary>
public static class GraficoDeBarras
{
    private const int Largura = 640;
    private const int AlturaDaLinha = 30;
    private const int MargemEsquerda = 190;
    private const int MargemDireita = 90;
    private const int AlturaDaBarra = 18;
    private const int MaximoDeCaracteresNoRotulo = 26;

    public sealed record Barra(string Rotulo, double Valor, string ValorTexto);

    /// <summary>SVG pronto para o HTML; as barras escalam em relação ao maior valor.</summary>
    public static string Svg(string titulo, string descricao, IReadOnlyList<Barra> barras)
    {
        var altura = Math.Max(barras.Count, 1) * AlturaDaLinha + 8;
        var maximo = barras.Count == 0 ? 0 : barras.Max(b => b.Valor);
        var larguraUtil = Largura - MargemEsquerda - MargemDireita;

        var svg = new StringBuilder();
        svg.Append(CultureInfo.InvariantCulture, $"<svg class=\"grafico\" viewBox=\"0 0 {Largura} {altura}\" role=\"img\" aria-labelledby=\"grafico-titulo grafico-descricao\">");
        svg.Append("<title id=\"grafico-titulo\">").Append(WebUtility.HtmlEncode(titulo)).Append("</title>");
        svg.Append("<desc id=\"grafico-descricao\">").Append(WebUtility.HtmlEncode(descricao)).Append("</desc>");

        for (var i = 0; i < barras.Count; i++)
        {
            var barra = barras[i];
            var topo = (i * AlturaDaLinha) + 4;
            var meio = topo + (AlturaDaBarra / 2) + 5;
            var comprimento = maximo <= 0 ? 0 : Math.Max(barra.Valor / maximo * larguraUtil, barra.Valor > 0 ? 2 : 0);

            svg.Append(CultureInfo.InvariantCulture, $"<text class=\"grafico-rotulo\" x=\"{MargemEsquerda - 8}\" y=\"{meio}\" text-anchor=\"end\">")
                .Append(WebUtility.HtmlEncode(Abreviar(barra.Rotulo))).Append("</text>");
            svg.Append(CultureInfo.InvariantCulture, $"<rect class=\"grafico-barra\" x=\"{MargemEsquerda}\" y=\"{topo}\" width=\"{comprimento:0.##}\" height=\"{AlturaDaBarra}\" rx=\"3\" />");
            svg.Append(CultureInfo.InvariantCulture, $"<text class=\"grafico-valor\" x=\"{MargemEsquerda + comprimento + 6:0.##}\" y=\"{meio}\">")
                .Append(WebUtility.HtmlEncode(barra.ValorTexto)).Append("</text>");
        }

        return svg.Append("</svg>").ToString();
    }

    private static string Abreviar(string texto) =>
        texto.Length <= MaximoDeCaracteresNoRotulo ? texto : texto[..(MaximoDeCaracteresNoRotulo - 1)] + "…";
}
