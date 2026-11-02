namespace Aurora.Application.Relatorios;

/// <summary>Como uma coluna é formatada nos arquivos exportados (PDF e Excel).</summary>
public enum TipoDeColuna
{
    Texto,
    Inteiro,

    /// <summary>Quantidade de estoque, com até 3 casas decimais.</summary>
    Quantidade,
    Moeda,
    Data,
    DataHora,

    /// <summary>Valor de 0 a 100.</summary>
    Percentual,
}

public sealed record ColunaDeRelatorio(string Titulo, TipoDeColuna Tipo = TipoDeColuna.Texto);

/// <summary>
/// Uma tabela do relatório. As células são tipadas (<c>int</c>, <c>decimal</c>, <c>DateOnly</c>, <c>DateTime</c>,
/// <c>bool</c>, <c>string</c> ou nulo) e cada exportador as formata do seu jeito: no Excel viram números e datas de
/// verdade (dá para somar e filtrar), no PDF viram texto em pt-BR.
/// </summary>
public sealed record SecaoDeRelatorio(
    string Titulo,
    IReadOnlyList<ColunaDeRelatorio> Colunas,
    IReadOnlyList<IReadOnlyList<object?>> Linhas,
    IReadOnlyList<object?>? Totais = null,
    string? Aviso = null);

/// <summary>Conteúdo de um relatório, independente do formato de saída (é o que o <see cref="IExportadorDeRelatorio"/> recebe).</summary>
public sealed record Relatorio(
    string Titulo,
    DateTime GeradoEmUtc,
    IReadOnlyList<string> Filtros,
    IReadOnlyList<SecaoDeRelatorio> Secoes);

/// <summary>Arquivo pronto para download.</summary>
public sealed record ArquivoGerado(byte[] Conteudo, string TipoDeConteudo, string NomeDoArquivo);

/// <summary>Filtros aceitos pelos relatórios; cada tipo usa os que fazem sentido para ele.</summary>
public sealed record FiltroDeRelatorio(DateOnly? De, DateOnly? Ate, string? Status, int? Ano);

/// <summary>Itens devolvidos pela consulta e quantos existiam, para o relatório avisar quando corta (limite de linhas).</summary>
public sealed record ResultadoLimitado<T>(IReadOnlyList<T> Itens, int Total);

/// <summary>Exporta um <see cref="Relatorio"/> para um formato de arquivo (Strategy: PDF e Excel são intercambiáveis).</summary>
public interface IExportadorDeRelatorio
{
    /// <summary>Nome do formato na rota: "pdf" ou "xlsx".</summary>
    string Formato { get; }

    string TipoDeConteudo { get; }

    string Extensao { get; }

    byte[] Exportar(Relatorio relatorio);
}

/// <summary>Escolhe o exportador pelo formato pedido (Factory), respeitando os formatos habilitados na configuração.</summary>
public interface IExportadorFactory
{
    IReadOnlyCollection<string> FormatosDisponiveis { get; }

    /// <summary>Devolve o exportador do formato; nulo se o formato não existe ou está desligado.</summary>
    IExportadorDeRelatorio? Criar(string? formato);
}
