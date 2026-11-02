using Aurora.Application.Relatorios;
using ClosedXML.Excel;

namespace Aurora.Infrastructure.Relatorios;

/// <summary>
/// Relatório em Excel (.xlsx): uma planilha por seção, com números e datas de verdade (dá para somar, ordenar e
/// filtrar), cabeçalho fixo e filtro automático. Texto é sempre gravado como texto: um valor que comece com "=" fica
/// literal e nunca vira fórmula (injeção de fórmula).
/// </summary>
public sealed class ExportadorExcel : IExportadorDeRelatorio
{
    private const int LinhaDoCabecalho = 5;
    private const int LarguraMinima = 8;
    private const int LarguraMaxima = 48;

    public string Formato => "xlsx";

    public string TipoDeConteudo => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public string Extensao => "xlsx";

    public byte[] Exportar(Relatorio relatorio)
    {
        using var pasta = new XLWorkbook();
        pasta.Properties.Title = relatorio.Titulo;
        pasta.Properties.Author = "Aurora Cadeiras";

        var nomesUsados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var secao in relatorio.Secoes)
        {
            var planilha = pasta.Worksheets.Add(NomeDaPlanilha(secao.Titulo, nomesUsados));
            Escrever(planilha, relatorio, secao);
        }

        using var memoria = new MemoryStream();
        pasta.SaveAs(memoria);
        return memoria.ToArray();
    }

    private static void Escrever(IXLWorksheet planilha, Relatorio relatorio, SecaoDeRelatorio secao)
    {
        planilha.Cell(1, 1).Value = relatorio.Titulo;
        planilha.Cell(1, 1).Style.Font.Bold = true;
        planilha.Cell(1, 1).Style.Font.FontSize = 14;
        planilha.Cell(2, 1).Value = string.Join(" | ", relatorio.Filtros);
        planilha.Cell(3, 1).Value = FormatadorDeCelulas.GeradoEm(relatorio.GeradoEmUtc);
        if (secao.Aviso is not null)
        {
            planilha.Cell(4, 1).Value = secao.Aviso;
            planilha.Cell(4, 1).Style.Font.Italic = true;
        }

        for (var c = 0; c < secao.Colunas.Count; c++)
        {
            var cabecalho = planilha.Cell(LinhaDoCabecalho, c + 1);
            cabecalho.Value = secao.Colunas[c].Titulo;
            cabecalho.Style.Font.Bold = true;
            cabecalho.Style.Font.FontColor = XLColor.White;
            cabecalho.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F3A5F");
            cabecalho.Style.Alignment.Horizontal = FormatadorDeCelulas.AlinhaADireita(secao.Colunas[c].Tipo)
                ? XLAlignmentHorizontalValues.Right
                : XLAlignmentHorizontalValues.Left;
        }

        var linha = LinhaDoCabecalho + 1;
        foreach (var valores in secao.Linhas)
        {
            EscreverLinha(planilha, secao, valores, linha++, totais: false);
        }

        if (secao.Totais is not null)
        {
            EscreverLinha(planilha, secao, secao.Totais, linha, totais: true);
        }

        var ultimaLinhaDeDados = LinhaDoCabecalho + Math.Max(secao.Linhas.Count, 1);
        planilha.SheetView.FreezeRows(LinhaDoCabecalho);
        if (secao.Linhas.Count > 0)
        {
            planilha.Range(LinhaDoCabecalho, 1, ultimaLinhaDeDados, secao.Colunas.Count).SetAutoFilter();
        }

        AjustarLarguras(planilha, secao);
    }

    private static void EscreverLinha(IXLWorksheet planilha, SecaoDeRelatorio secao, IReadOnlyList<object?> valores, int linha, bool totais)
    {
        for (var c = 0; c < secao.Colunas.Count; c++)
        {
            var tipo = secao.Colunas[c].Tipo;
            var celula = planilha.Cell(linha, c + 1);
            DefinirValor(celula, c < valores.Count ? valores[c] : null, tipo);
            if (totais)
            {
                celula.Style.Font.Bold = true;
                celula.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            }
        }
    }

    private static void DefinirValor(IXLCell celula, object? valor, TipoDeColuna tipo)
    {
        switch (valor)
        {
            case null:
                return;
            case bool b:
                celula.Value = b ? "Sim" : "Não";
                return;
            case string texto:
                // Texto entra como texto, nunca como fórmula
                celula.Value = texto;
                celula.Style.NumberFormat.Format = "@";
                return;
            case DateOnly data:
                celula.Value = data.ToDateTime(TimeOnly.MinValue);
                celula.Style.NumberFormat.Format = "dd/mm/yyyy";
                return;
            case DateTime momento:
                celula.Value = tipo == TipoDeColuna.Data ? momento.Date : FormatadorDeCelulas.ParaHorarioLocal(momento);
                celula.Style.NumberFormat.Format = tipo == TipoDeColuna.Data ? "dd/mm/yyyy" : "dd/mm/yyyy hh:mm";
                return;
            case decimal numero:
                // Percentual: o Excel guarda a fração (12,5% é 0,125) e formata como porcentagem
                celula.Value = tipo == TipoDeColuna.Percentual ? numero / 100m : numero;
                celula.Style.NumberFormat.Format = tipo switch
                {
                    TipoDeColuna.Moeda => "\"R$\" #,##0.00",
                    TipoDeColuna.Percentual => "0.00%",
                    TipoDeColuna.Quantidade => "#,##0.000",
                    _ => "#,##0.00",
                };
                return;
            case int inteiro:
                celula.Value = inteiro;
                celula.Style.NumberFormat.Format = "#,##0";
                return;
            case long longo:
                celula.Value = longo;
                celula.Style.NumberFormat.Format = "#,##0";
                return;
            case double real:
                celula.Value = real;
                celula.Style.NumberFormat.Format = "#,##0.00";
                return;
            default:
                celula.Value = valor.ToString() ?? "";
                return;
        }
    }

    /// <summary>
    /// Largura pela quantidade de caracteres do maior texto da coluna. Não usa o ajuste automático do Excel/ClosedXML
    /// porque ele mede com fontes do sistema, que o contêiner Alpine não tem.
    /// </summary>
    private static void AjustarLarguras(IXLWorksheet planilha, SecaoDeRelatorio secao)
    {
        for (var c = 0; c < secao.Colunas.Count; c++)
        {
            var maior = secao.Colunas[c].Titulo.Length;
            foreach (var linha in secao.Linhas.Take(300).Append(secao.Totais ?? []))
            {
                if (c < linha.Count)
                {
                    maior = Math.Max(maior, FormatadorDeCelulas.Texto(linha[c], secao.Colunas[c].Tipo).Length);
                }
            }

            planilha.Column(c + 1).Width = Math.Clamp(maior + 3, LarguraMinima, LarguraMaxima);
        }
    }

    /// <summary>Nome de planilha válido: até 31 caracteres, sem [ ] : * ? / \ e sem repetir.</summary>
    private static string NomeDaPlanilha(string titulo, HashSet<string> usados)
    {
        var limpo = new string(titulo.Where(c => !"[]:*?/\\".Contains(c, StringComparison.Ordinal)).ToArray()).Trim();
        var nome = limpo.Length == 0 ? "Planilha" : limpo[..Math.Min(limpo.Length, 31)];
        var tentativa = nome;
        for (var i = 2; !usados.Add(tentativa); i++)
        {
            var sufixo = $" ({i})";
            tentativa = nome[..Math.Min(nome.Length, 31 - sufixo.Length)] + sufixo;
        }

        return tentativa;
    }
}
