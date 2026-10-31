namespace Aurora.Web.Api.Producao;

public sealed record EtapaResposta(
    int Id,
    int OrdemProducaoId,
    string NumeroOp,
    byte EstacaoId,
    string EstacaoNome,
    byte Sequencia,
    string Status,
    DateTime? IniciadaEm,
    DateTime? ConcluidaEm,
    int? OperadorId,
    string? OperadorNome);
