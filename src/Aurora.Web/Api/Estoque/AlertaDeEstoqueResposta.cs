namespace Aurora.Web.Api.Estoque;

public sealed record AlertaDeEstoqueResposta(
    int Id,
    int MateriaPrimaId,
    string MateriaPrimaCodigo,
    string MateriaPrimaNome,
    string Unidade,
    decimal EstoqueAtual,
    decimal EstoqueMinimo,
    DateTime CriadoEm,
    DateTime? LidoEm,
    int? LidoPorId);
