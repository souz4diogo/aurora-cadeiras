using Aurora.Domain.Enums;

namespace Aurora.Application.Relatorios;

/// <summary>Filtros que a tela de relatórios oferece para um tipo.</summary>
[Flags]
public enum FiltrosDoRelatorio
{
    Nenhum = 0,
    Periodo = 1,
    Status = 2,
    Ano = 4,
}

/// <summary>
/// Um tipo de relatório (Strategy): sabe quem pode pedi-lo, que filtros aceita e como montar o <see cref="Relatorio"/>.
/// Para criar um relatório novo basta uma nova implementação; o serviço e os exportadores não mudam.
/// </summary>
public interface IGeradorDeRelatorio
{
    /// <summary>Nome na rota: <c>/relatorios/{tipo}</c>.</summary>
    string Tipo { get; }

    string Titulo { get; }

    FiltrosDoRelatorio Filtros { get; }

    /// <summary>Perfis que podem gerar este relatório, conforme a matriz do docs/06 §3.</summary>
    IReadOnlySet<PerfilUsuario> Perfis { get; }

    Task<Relatorio> GerarAsync(FiltroDeRelatorio filtro, DateTime agoraUtc, CancellationToken ct = default);
}

/// <summary>Constantes e atalhos que os geradores compartilham.</summary>
internal static class ConstantesDeRelatorio
{
    /// <summary>Máximo de linhas por tabela: acima disso o relatório avisa e orienta a filtrar.</summary>
    public const int LimiteDeLinhas = 5_000;

    public static string? AvisoDeCorte(int exibidas, int total) =>
        total > exibidas
            ? $"Exibindo {exibidas:N0} de {total:N0} linhas. Use os filtros para restringir o relatório."
            : null;

    public static string Periodo(DateOnly? de, DateOnly? ate) => (de, ate) switch
    {
        (not null, not null) => $"Período: {de:dd/MM/yyyy} a {ate:dd/MM/yyyy}",
        (not null, null) => $"A partir de {de:dd/MM/yyyy}",
        (null, not null) => $"Até {ate:dd/MM/yyyy}",
        _ => "Período: todos",
    };
}
