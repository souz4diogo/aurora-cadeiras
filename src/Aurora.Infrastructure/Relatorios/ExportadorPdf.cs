using Aurora.Application.Relatorios;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Aurora.Infrastructure.Relatorios;

/// <summary>Relatório em PDF (A4 paisagem), com título, filtros, tabelas de cabeçalho repetido e numeração de páginas.</summary>
public sealed class ExportadorPdf : IExportadorDeRelatorio
{
    private const string CorDoCabecalho = "#1F3A5F";
    private const string CorDaLinhaAlternada = "#F3F5F8";

    public string Formato => "pdf";

    public string TipoDeConteudo => "application/pdf";

    public string Extensao => "pdf";

    public byte[] Exportar(Relatorio relatorio)
    {
        FontesDoRelatorio.Garantir();

        return Document.Create(documento => documento.Page(pagina =>
            {
                pagina.Size(PageSizes.A4.Landscape());
                pagina.Margin(28);
                pagina.DefaultTextStyle(estilo => estilo.FontFamily(FontesDoRelatorio.Familia).FontSize(9));

                pagina.Header().Element(cabecalho => Cabecalho(cabecalho, relatorio));
                pagina.Content().PaddingTop(10).Column(coluna =>
                {
                    coluna.Spacing(16);
                    foreach (var secao in relatorio.Secoes)
                    {
                        coluna.Item().Element(item => Secao(item, secao));
                    }
                });
                pagina.Footer().Row(rodape =>
                {
                    rodape.RelativeItem().Text("Aurora Cadeiras").FontSize(8).FontColor(Colors.Grey.Darken1);
                    rodape.RelativeItem().AlignRight().Text(texto =>
                    {
                        texto.DefaultTextStyle(estilo => estilo.FontSize(8).FontColor(Colors.Grey.Darken1));
                        texto.Span("Página ");
                        texto.CurrentPageNumber();
                        texto.Span(" de ");
                        texto.TotalPages();
                    });
                });
            }))
            .WithMetadata(new DocumentMetadata
            {
                Title = relatorio.Titulo,
                Author = "Aurora Cadeiras",
                Creator = "Aurora Cadeiras",
                Language = "pt-BR",
                CreationDate = DateTimeOffset.UtcNow,
            })
            .GeneratePdf();
    }

    private static void Cabecalho(IContainer container, Relatorio relatorio) =>
        container.PaddingBottom(6).BorderBottom(1).BorderColor(CorDoCabecalho).Column(coluna =>
        {
            coluna.Item().Text("Aurora Cadeiras").FontSize(9).FontColor(Colors.Grey.Darken1);
            coluna.Item().Text(relatorio.Titulo).FontSize(17).Bold().FontColor(CorDoCabecalho);
            foreach (var filtro in relatorio.Filtros)
            {
                coluna.Item().Text(filtro).FontSize(9);
            }

            coluna.Item().Text(FormatadorDeCelulas.GeradoEm(relatorio.GeradoEmUtc)).FontSize(8).FontColor(Colors.Grey.Darken1);
        });

    private static void Secao(IContainer container, SecaoDeRelatorio secao) =>
        container.Column(coluna =>
        {
            coluna.Spacing(4);
            coluna.Item().Text(secao.Titulo).FontSize(12).Bold().FontColor(CorDoCabecalho);
            if (secao.Aviso is not null)
            {
                coluna.Item().Text(secao.Aviso).Italic().FontColor(Colors.Red.Darken2);
            }

            if (secao.Linhas.Count == 0)
            {
                coluna.Item().Text("Nenhum registro para os filtros informados.").Italic();
                return;
            }

            coluna.Item().Table(tabela => Tabela(tabela, secao));
        });

    private static void Tabela(TableDescriptor tabela, SecaoDeRelatorio secao)
    {
        tabela.ColumnsDefinition(colunas =>
        {
            for (var i = 0; i < secao.Colunas.Count; i++)
            {
                colunas.RelativeColumn(PesoDaColuna(secao, i));
            }
        });

        tabela.Header(cabecalho =>
        {
            foreach (var coluna in secao.Colunas)
            {
                var celula = cabecalho.Cell().Background(CorDoCabecalho).Padding(4);
                Alinhar(celula, coluna.Tipo)
                    .Text(coluna.Titulo).Bold().FontColor(Colors.White);
            }
        });

        for (var l = 0; l < secao.Linhas.Count; l++)
        {
            var fundo = l % 2 == 1 ? Color.FromHex(CorDaLinhaAlternada) : Colors.White;
            Celulas(tabela, secao, secao.Linhas[l], fundo, negrito: false);
        }

        if (secao.Totais is not null)
        {
            Celulas(tabela, secao, secao.Totais, Colors.Grey.Lighten2, negrito: true);
        }
    }

    private static void Celulas(
        TableDescriptor tabela, SecaoDeRelatorio secao, IReadOnlyList<object?> valores, Color fundo, bool negrito)
    {
        for (var c = 0; c < secao.Colunas.Count; c++)
        {
            var tipo = secao.Colunas[c].Tipo;
            var texto = FormatadorDeCelulas.Texto(c < valores.Count ? valores[c] : null, tipo);
            var celula = Alinhar(tabela.Cell().Background(fundo).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4), tipo);
            var descritor = celula.Text(texto);
            if (negrito)
            {
                descritor.Bold();
            }
        }
    }

    private static IContainer Alinhar(IContainer celula, TipoDeColuna tipo) =>
        FormatadorDeCelulas.AlinhaADireita(tipo) ? celula.AlignRight() : celula;

    /// <summary>Largura relativa pelo maior texto da coluna (limitada), para colunas de texto não espremerem as de número.</summary>
    private static float PesoDaColuna(SecaoDeRelatorio secao, int indice)
    {
        var maior = secao.Colunas[indice].Titulo.Length;
        foreach (var linha in secao.Linhas.Take(200))
        {
            if (indice < linha.Count)
            {
                maior = Math.Max(maior, FormatadorDeCelulas.Texto(linha[indice], secao.Colunas[indice].Tipo).Length);
            }
        }

        return Math.Clamp(maior, 7, 34);
    }
}
