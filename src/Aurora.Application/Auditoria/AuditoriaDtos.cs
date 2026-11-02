namespace Aurora.Application.Auditoria;

/// <summary>Filtros do histórico; as datas são dias (UTC) inclusivos.</summary>
public sealed record FiltroDeAuditoria(string? Tabela, string? Registro, int? UsuarioId, DateOnly? De, DateOnly? Ate);

/// <summary>Linha do histórico (leitura), já com o nome de quem alterou.</summary>
public sealed record AuditoriaResumo(
    long Id,
    string Tabela,
    string Registro,
    string Acao,
    int? UsuarioId,
    string? UsuarioNome,
    DateTime DataHora,
    string? Antes,
    string? Depois);

/// <summary>Um campo que mudou. Nulos significam "não havia valor" (inclusão) ou "deixou de existir" (exclusão).</summary>
public sealed record CampoAlteradoDto(string Campo, string? Antes, string? Depois);

/// <param name="Acao">Inclusão, Alteração ou Exclusão.</param>
/// <param name="Alteracoes">Na alteração, só os campos que mudaram; na inclusão e na exclusão, todos.</param>
public sealed record AuditoriaDto(
    long Id,
    string Tabela,
    string Registro,
    string Acao,
    int? UsuarioId,
    string? UsuarioNome,
    DateTime DataHora,
    IReadOnlyList<CampoAlteradoDto> Alteracoes);
