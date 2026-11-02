using Aurora.Application.Relatorios;
using Aurora.Infrastructure.Relatorios;
using ClosedXML.Excel;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;

namespace Aurora.Api.IntegrationTests;

/// <summary>Os exportadores com casos de borda que a API real raramente produz: muitas páginas, vazio, nomes estranhos.</summary>
public class ExportadoresTests
{
    private static readonly DateTime GeradoEm = new(2026, 11, 2, 15, 0, 0, DateTimeKind.Utc);

    private static Relatorio Relatorio(params SecaoDeRelatorio[] secoes) =>
        new("Relatório de teste", GeradoEm, ["Filtro de exemplo"], secoes);

    private static SecaoDeRelatorio Tabela(int linhas) => new(
        "Itens",
        [new("Número"), new("Valor", TipoDeColuna.Moeda)],
        Enumerable.Range(1, linhas).Select(i => (IReadOnlyList<object?>)[$"ITEM-{i:D4}", i * 1.5m]).ToList(),
        Totais: ["Total", linhas * 1.5m]);

    private static string Texto(byte[] pdf)
    {
        using var documento = PdfDocument.Open(pdf);
        return System.Text.RegularExpressions.Regex.Replace(
            string.Join(" ", documento.GetPages().Select(p => p.Text)).Replace(' ', ' '), @"\s+", " ");
    }

    [Fact]
    public void Pdf_usa_a_fonte_embutida_Roboto_e_nao_uma_do_sistema()
    {
        var pdf = new ExportadorPdf().Exportar(Relatorio(Tabela(3)));

        using var documento = PdfDocument.Open(pdf);
        var fontes = documento.GetPages().SelectMany(p => p.Letters).Select(l => l.FontName).Distinct().ToList();
        Assert.NotEmpty(fontes);
        Assert.All(fontes, nome => Assert.Contains("Roboto", nome, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Pdf_com_muitas_linhas_vira_varias_paginas_com_cabecalho_repetido_e_numeracao()
    {
        var pdf = new ExportadorPdf().Exportar(Relatorio(Tabela(600)));

        using var documento = PdfDocument.Open(pdf);
        Assert.True(documento.NumberOfPages >= 5);
        Assert.All(documento.GetPages(), pagina => Assert.Contains("Número", pagina.Text, StringComparison.Ordinal));
        Assert.Contains($"Página {documento.NumberOfPages} de {documento.NumberOfPages}", Texto(pdf), StringComparison.Ordinal);
        Assert.Contains("ITEM-0600", Texto(pdf), StringComparison.Ordinal);
    }

    [Fact]
    public void Pdf_formata_valores_em_pt_BR_e_converte_a_hora_para_Brasilia()
    {
        var secao = new SecaoDeRelatorio(
            "Formatos",
            [
                new("Dinheiro", TipoDeColuna.Moeda),
                new("Percentual", TipoDeColuna.Percentual),
                new("Quantidade", TipoDeColuna.Quantidade),
                new("Inteiro", TipoDeColuna.Inteiro),
                new("Data", TipoDeColuna.Data),
                new("Momento", TipoDeColuna.DataHora),
                new("Sim ou não"),
                new("Vazio"),
            ],
            [[1290m, 12.5m, 4.125m, 1234567, new DateOnly(2026, 11, 2), GeradoEm, true, null]]);

        var texto = Texto(new ExportadorPdf().Exportar(Relatorio(secao)));

        Assert.Contains("R$ 1.290,00", texto, StringComparison.Ordinal);
        Assert.Contains("12,50%", texto, StringComparison.Ordinal);
        Assert.Contains("4,125", texto, StringComparison.Ordinal);
        Assert.Contains("1.234.567", texto, StringComparison.Ordinal);
        Assert.Contains("02/11/2026", texto, StringComparison.Ordinal);
        Assert.Contains("02/11/2026 12:00", texto, StringComparison.Ordinal); // 15:00 UTC = 12:00 em Brasília
        Assert.Contains("Gerado em 02/11/2026 12:00 (horário de Brasília)", texto, StringComparison.Ordinal);
        Assert.Contains("Sim", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Pdf_de_secao_vazia_diz_que_nao_ha_registro_e_mostra_o_aviso()
    {
        var vazia = new SecaoDeRelatorio("Vazia", [new("Coluna")], [], Aviso: "Exibindo 0 de 9.999 linhas.");

        var texto = Texto(new ExportadorPdf().Exportar(Relatorio(vazia)));

        Assert.Contains("Nenhum registro para os filtros informados.", texto, StringComparison.Ordinal);
        Assert.Contains("Exibindo 0 de 9.999 linhas.", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void Pdf_aguenta_texto_longo_acentos_e_simbolos_sem_quebrar()
    {
        var longo = string.Concat(Enumerable.Repeat("Ação de produção ", 200)) + "<b>&amp;</b> \"aspas\" ç ã é ü ñ";
        var secao = new SecaoDeRelatorio("Texto", [new("Descrição")], [[longo]]);

        var pdf = new ExportadorPdf().Exportar(Relatorio(secao));

        Assert.Contains("Ação de produção", Texto(pdf), StringComparison.Ordinal);
        Assert.Contains("<b>&amp;</b>", Texto(pdf), StringComparison.Ordinal); // o texto sai literal, não é interpretado
    }

    [Fact]
    public void Pdf_traz_o_titulo_nos_metadados_do_documento()
    {
        var pdf = new ExportadorPdf().Exportar(Relatorio(Tabela(1)));

        using var documento = PdfDocument.Open(pdf);
        Assert.Equal("Relatório de teste", documento.Information.Title);
        Assert.Equal("Aurora Cadeiras", documento.Information.Author);
    }

    [Fact]
    public void Excel_guarda_cada_tipo_como_o_tipo_certo_com_o_formato_certo()
    {
        var secao = new SecaoDeRelatorio(
            "Tipos",
            [
                new("Dinheiro", TipoDeColuna.Moeda),
                new("Percentual", TipoDeColuna.Percentual),
                new("Quantidade", TipoDeColuna.Quantidade),
                new("Inteiro", TipoDeColuna.Inteiro),
                new("Data", TipoDeColuna.Data),
                new("Momento", TipoDeColuna.DataHora),
                new("Texto"),
            ],
            [[1290.5m, 12.5m, 4.125m, 7, new DateOnly(2026, 11, 2), GeradoEm, "ok"]]);

        using var pasta = new XLWorkbook(new MemoryStream(new ExportadorExcel().Exportar(Relatorio(secao))));

        var linha = pasta.Worksheet("Tipos").Row(6);
        Assert.Equal(1290.5m, linha.Cell(1).GetValue<decimal>());
        Assert.Contains("R$", linha.Cell(1).Style.NumberFormat.Format, StringComparison.Ordinal);
        Assert.Equal(0.125m, linha.Cell(2).GetValue<decimal>()); // 12,5% é a fração 0,125
        Assert.Equal("0.00%", linha.Cell(2).Style.NumberFormat.Format);
        Assert.Equal(4.125m, linha.Cell(3).GetValue<decimal>());
        Assert.Equal("#,##0.000", linha.Cell(3).Style.NumberFormat.Format);
        Assert.Equal(7, linha.Cell(4).GetValue<int>());
        Assert.Equal(new DateTime(2026, 11, 2), linha.Cell(5).GetDateTime());
        Assert.Equal(new DateTime(2026, 11, 2, 12, 0, 0), linha.Cell(6).GetDateTime()); // 15:00 UTC = 12:00 em Brasília
        Assert.Equal("ok", linha.Cell(7).GetString());
    }

    [Fact]
    public void Excel_tem_totais_em_negrito_e_larguras_de_coluna_dentro_de_limites()
    {
        using var pasta = new XLWorkbook(new MemoryStream(new ExportadorExcel().Exportar(Relatorio(Tabela(40)))));

        var planilha = pasta.Worksheet("Itens");
        var totais = planilha.Row(6 + 40);
        Assert.Equal("Total", totais.Cell(1).GetString());
        Assert.Equal(60m, totais.Cell(2).GetValue<decimal>()); // 40 × 1,5
        Assert.True(totais.Cell(1).Style.Font.Bold);
        Assert.InRange(planilha.Column(1).Width, 8, 48);
        Assert.InRange(planilha.Column(2).Width, 8, 48);
        Assert.True(planilha.AutoFilter.IsEnabled);
    }

    [Fact]
    public void Excel_de_secao_vazia_tem_cabecalho_mas_nao_liga_o_filtro()
    {
        var vazia = new SecaoDeRelatorio("Vazia", [new("Coluna A"), new("Coluna B")], [], Aviso: "Sem dados");

        using var pasta = new XLWorkbook(new MemoryStream(new ExportadorExcel().Exportar(Relatorio(vazia))));

        var planilha = pasta.Worksheet("Vazia");
        Assert.Equal("Coluna A", planilha.Cell(5, 1).GetString());
        Assert.Equal("Sem dados", planilha.Cell(4, 1).GetString());
        Assert.False(planilha.AutoFilter.IsEnabled);
    }

    [Fact]
    public void Excel_limpa_nomes_de_planilha_invalidos_e_repetidos_e_respeita_o_limite_de_31_caracteres()
    {
        var secoes = new[]
        {
            new SecaoDeRelatorio("Pedidos/2026: [parcial]*?", [new("A")], [["x"]]),
            new SecaoDeRelatorio("Pedidos2026 parcial", [new("A")], [["y"]]),
            new SecaoDeRelatorio("Pedidos2026 parcial", [new("A")], [["z"]]),
            new SecaoDeRelatorio(new string('N', 80), [new("A")], [["w"]]),
            new SecaoDeRelatorio("[]:*?/\\", [new("A")], [["v"]]),
        };

        using var pasta = new XLWorkbook(new MemoryStream(new ExportadorExcel().Exportar(Relatorio(secoes))));

        var nomes = pasta.Worksheets.Select(w => w.Name).ToList();
        Assert.Equal(5, nomes.Count);
        Assert.Equal(nomes.Count, nomes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(nomes, nome =>
        {
            Assert.InRange(nome.Length, 1, 31);
            Assert.DoesNotContain(nome, c => "[]:*?/\\".Contains(c, StringComparison.Ordinal));
        });
        Assert.Contains("Planilha", nomes); // o que sobrou vazio ganhou um nome padrão
    }

    [Fact]
    public void Excel_com_varias_secoes_cria_uma_planilha_para_cada()
    {
        using var pasta = new XLWorkbook(new MemoryStream(new ExportadorExcel().Exportar(Relatorio(Tabela(2), Tabela(3)))));

        Assert.Equal(2, pasta.Worksheets.Count);
        Assert.Equal("Relatório de teste", pasta.Properties.Title);
    }

    [Fact]
    public void Texto_que_comeca_com_sinal_de_formula_continua_texto()
    {
        var secao = new SecaoDeRelatorio("Perigo", [new("Valor")], [["=1+1"], ["+cmd|' /C calc'!A0"], ["@SUM(1)"], ["-2+3"]]);

        using var pasta = new XLWorkbook(new MemoryStream(new ExportadorExcel().Exportar(Relatorio(secao))));

        var planilha = pasta.Worksheet("Perigo");
        for (var linha = 6; linha <= 9; linha++)
        {
            var celula = planilha.Cell(linha, 1);
            Assert.False(celula.HasFormula);
            Assert.Equal(XLDataType.Text, celula.DataType);
        }

        Assert.Equal("=1+1", planilha.Cell(6, 1).GetString());
    }

    [Theory]
    [InlineData(new string[0], "pdf", true)]
    [InlineData(new string[0], "xlsx", true)]
    [InlineData(new[] { "pdf" }, "pdf", true)]
    [InlineData(new[] { "pdf" }, "xlsx", false)]
    [InlineData(new[] { "XLSX" }, "xlsx", true)]
    [InlineData(new[] { "XLSX" }, "pdf", false)]
    public void Fabrica_respeita_os_formatos_habilitados_sem_diferenciar_maiusculas(string[] habilitados, string pedido, bool existe)
    {
        var fabrica = new ExportadorFactory(
            [new ExportadorPdf(), new ExportadorExcel()], Options.Create(new RelatoriosOptions { Formatos = habilitados }));

        Assert.Equal(existe, fabrica.Criar(pedido) is not null);
        Assert.Equal(existe, fabrica.Criar(pedido.ToUpperInvariant()) is not null);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("docx")]
    public void Fabrica_devolve_nulo_para_formato_vazio_ou_desconhecido(string? formato)
    {
        var fabrica = new ExportadorFactory([new ExportadorPdf(), new ExportadorExcel()], Options.Create(new RelatoriosOptions()));

        Assert.Null(fabrica.Criar(formato));
        Assert.Equal(["pdf", "xlsx"], fabrica.FormatosDisponiveis.Order());
    }
}
