namespace Aurora.Infrastructure.Relatorios;

/// <summary>
/// Os relatórios saem em pt-BR (R$ 1.290,00, "novembro de 2026"). Em imagens enxutas (Alpine sem os dados completos do
/// ICU) o .NET cria a cultura pt-BR sem os dados e passa a formatar como inglês, sem avisar: o PDF sairia com
/// "R$1,290.00" e "November". Esta verificação faz a API falhar na inicialização em vez de gerar relatório errado.
/// </summary>
public static class CulturaDosRelatorios
{
    /// <summary>Lança se a cultura pt-BR não estiver disponível de verdade neste ambiente.</summary>
    public static void Garantir()
    {
        var cultura = FormatadorDeCelulas.PtBr;
        var numero = 1234.5m.ToString("N1", cultura);
        var mes = cultura.DateTimeFormat.GetMonthName(11);
        var moeda = 1290m.ToString("C2", cultura).Replace((char)0x00A0, (char)0x20);
        if (numero != "1.234,5" || mes != "novembro" || moeda != "R$ 1.290,00")
        {
            throw new InvalidOperationException(
                "A cultura pt-BR não está disponível neste ambiente (faltam os dados do ICU): " +
                $"número \"{numero}\", mês \"{mes}\", moeda \"{moeda}\". Na imagem Alpine, instale o pacote icu-data-full.");
        }
    }
}
