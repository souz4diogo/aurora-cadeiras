using System.Reflection;
using QuestPDF.Drawing;
using QuestPDF.Infrastructure;

namespace Aurora.Infrastructure.Relatorios;

/// <summary>
/// Prepara o QuestPDF uma única vez: licença e fontes. A imagem Alpine do contêiner não tem nenhuma fonte instalada e
/// o QuestPDF não traz uma, então a Roboto (licença OFL, texto em <c>Relatorios/Fontes/LICENCA-OFL-Roboto.txt</c>) vai
/// embutida neste assembly e é registrada aqui. Sem isso o PDF sairia sem texto no contêiner.
/// </summary>
internal static class FontesDoRelatorio
{
    public const string Familia = "Roboto";

    private static readonly Lazy<bool> Preparado = new(Preparar);

    public static void Garantir() => _ = Preparado.Value;

    private static bool Preparar()
    {
        // Community: projetos de estudo e empresas pequenas (o PIM é acadêmico); revisar se o sistema virar produto
        QuestPDF.Settings.License = LicenseType.Community;

        var assembly = Assembly.GetExecutingAssembly();
        foreach (var recurso in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)))
        {
            using var fluxo = assembly.GetManifestResourceStream(recurso)
                ?? throw new InvalidOperationException($"Fonte embutida não encontrada: {recurso}");
            FontManager.RegisterFontFromStream(fluxo);
        }

        return true;
    }
}
