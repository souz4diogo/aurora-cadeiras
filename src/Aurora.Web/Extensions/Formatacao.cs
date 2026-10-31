using System.Globalization;

namespace Aurora.Web.Extensions;

/// <summary>Formatação de valores para exibição no padrão brasileiro (a cultura pt-BR é a da aplicação inteira).</summary>
public static class Formatacao
{
    public static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>
    /// Falha na inicialização se a cultura pt-BR não estiver disponível de verdade. Em imagens enxutas (Alpine sem
    /// os dados completos do ICU) o .NET cria a cultura sem os dados e passa a formatar como inglês: "R$1,290.00".
    /// </summary>
    public static void GarantirCulturaPtBr()
    {
        if (1234.5m.ToString("N1", PtBr) != "1.234,5")
        {
            throw new InvalidOperationException(
                "A cultura pt-BR não está disponível neste ambiente (faltam os dados do ICU). " +
                "Na imagem Alpine, instale o pacote icu-data-full.");
        }
    }

    public static string Moeda(decimal valor) => valor.ToString("C2", PtBr);

    /// <summary>Quantidades de estoque: até 3 casas, sem zeros à direita desnecessários.</summary>
    public static string Quantidade(decimal valor) => valor.ToString("#,##0.###", PtBr);

    /// <summary>Tira os zeros à direita que o banco devolve (2,500 vira 2,5) para o campo do formulário ficar legível.</summary>
    public static decimal SemZerosAMais(decimal valor) => valor / 1.000000000000000000000000000000000m;

    /// <summary>Nome do status do pedido como o usuário lê ("EmProducao" vira "Em produção").</summary>
    public static string StatusDoPedido(string status) => status switch
    {
        "EmProducao" => "Em produção",
        _ => status,
    };

    public static string StatusDaOrdem(string status) => status switch
    {
        "EmAndamento" => "Em andamento",
        "Concluida" => "Concluída",
        _ => status,
    };

    public static string StatusDaEtapa(string status) => StatusDaOrdem(status);

    public static string TipoDeMovimento(string tipo) => tipo switch
    {
        "Saida" => "Saída",
        _ => tipo,
    };

    public static string OrigemDoMovimento(string origem) => origem switch
    {
        "Producao" => "Produção",
        "ProducaoConcluida" => "Produção concluída",
        "Expedicao" => "Expedição",
        _ => origem,
    };

    /// <summary>Quantidade de movimento com o sinal explícito: "+10", "-2,5".</summary>
    public static string QuantidadeComSinal(decimal valor) => valor.ToString("+#,##0.###;-#,##0.###;0", PtBr);

    /// <summary>Duração em minutos como se lê no chão de fábrica: "45 min", "2 h 10 min", "1 d 3 h".</summary>
    public static string Duracao(int minutos) => minutos switch
    {
        < 60 => $"{minutos} min",
        < 24 * 60 => minutos % 60 == 0 ? $"{minutos / 60} h" : $"{minutos / 60} h {minutos % 60} min",
        _ => minutos % (24 * 60) / 60 == 0 ? $"{minutos / (24 * 60)} d" : $"{minutos / (24 * 60)} d {minutos % (24 * 60) / 60} h",
    };

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
