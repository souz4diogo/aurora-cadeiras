using System.Globalization;

namespace Aurora.Web.Extensions;

/// <summary>Formatação de valores para exibição no padrão brasileiro (a cultura pt-BR é a da aplicação inteira).</summary>
public static class Formatacao
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Moeda(decimal valor) => valor.ToString("C2", PtBr);

    /// <summary>Quantidades de estoque: até 3 casas, sem zeros à direita desnecessários.</summary>
    public static string Quantidade(decimal valor) => valor.ToString("#,##0.###", PtBr);

    /// <summary>Tira os zeros à direita que o banco devolve (2,500 vira 2,5) para o campo do formulário ficar legível.</summary>
    public static decimal SemZerosAMais(decimal valor) => valor / 1.000000000000000000000000000000000m;

    public static string Data(DateOnly data) => data.ToString("dd/MM/yyyy", PtBr);

    public static string DataHora(DateTime dataHoraUtc) => dataHoraUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", PtBr);

    /// <summary>CPF (000.000.000-00) ou CNPJ (00.000.000/0000-00) a partir dos dígitos; outro tamanho volta como veio.</summary>
    public static string Documento(string? digitos) => digitos?.Length switch
    {
        11 => $"{digitos[..3]}.{digitos[3..6]}.{digitos[6..9]}-{digitos[9..]}",
        14 => $"{digitos[..2]}.{digitos[2..5]}.{digitos[5..8]}/{digitos[8..12]}-{digitos[12..]}",
        _ => digitos ?? "",
    };
}
