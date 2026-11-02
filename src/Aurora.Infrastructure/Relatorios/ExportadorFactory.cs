using Aurora.Application.Relatorios;
using Microsoft.Extensions.Options;

namespace Aurora.Infrastructure.Relatorios;

/// <summary>Formatos de exportação ligados no ambiente (feature flag, docs/04): <c>Relatorios:Formatos</c>.</summary>
public sealed class RelatoriosOptions
{
    public const string Secao = "Relatorios";

    /// <summary>
    /// Quais exportadores ficam disponíveis (ex.: <c>Relatorios__Formatos__0=pdf</c> liga só o PDF). Vazio significa todos.
    /// O padrão é vazio de propósito: o binder de configuração do .NET mistura o array padrão com o configurado, e um
    /// padrão <c>["pdf","xlsx"]</c> manteria o Excel ligado mesmo configurando só o PDF.
    /// </summary>
    public string[] Formatos { get; set; } = [];
}

/// <summary>Factory: devolve o exportador do formato pedido, entre os que existem E estão habilitados (docs/04 §Padrões).</summary>
public sealed class ExportadorFactory(IEnumerable<IExportadorDeRelatorio> exportadores, IOptions<RelatoriosOptions> opcoes)
    : IExportadorFactory
{
    private readonly IReadOnlyList<IExportadorDeRelatorio> _habilitados = exportadores
        .Where(e => opcoes.Value.Formatos.Length == 0 || opcoes.Value.Formatos.Contains(e.Formato, StringComparer.OrdinalIgnoreCase))
        .ToList();

    public IReadOnlyCollection<string> FormatosDisponiveis => _habilitados.Select(e => e.Formato).ToList();

    public IExportadorDeRelatorio? Criar(string? formato) =>
        string.IsNullOrWhiteSpace(formato)
            ? null
            : _habilitados.FirstOrDefault(e => string.Equals(e.Formato, formato.Trim(), StringComparison.OrdinalIgnoreCase));
}
