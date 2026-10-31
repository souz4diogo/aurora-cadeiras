using System.Drawing;
using QRCoder;

namespace Aurora.Web.Extensions;

/// <summary>QR code da etiqueta da OP, gerado como SVG para sair nítido em qualquer tamanho de impressão.</summary>
public static class CodigoQr
{
    /// <summary>
    /// SVG pronto para entrar no HTML da página: escala com o contêiner (só <c>viewBox</c>), sem estilo embutido (a
    /// política de conteúdo do painel não aceita) e com texto alternativo para leitores de tela.
    /// </summary>
    public static string Svg(string conteudo, string descricao)
    {
        using var gerador = new QRCodeGenerator();
        using var dados = gerador.CreateQrCode(conteudo, QRCodeGenerator.ECCLevel.Q);
        var svg = new SvgQRCode(dados).GetGraphic(10, Color.Black, Color.White, drawQuietZones: true, SvgQRCode.SizingMode.ViewBoxAttribute);

        var rotulo = System.Net.WebUtility.HtmlEncode(descricao);
        return svg.Replace("<svg ", $"<svg role=\"img\" aria-label=\"{rotulo}\" ", StringComparison.Ordinal);
    }
}
