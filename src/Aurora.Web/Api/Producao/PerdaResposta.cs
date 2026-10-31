namespace Aurora.Web.Api.Producao;

public sealed record PerdaResposta(
    int Id,
    int EtapaId,
    string EstacaoNome,
    string Tipo,
    int Quantidade,
    string Motivo,
    int UsuarioId,
    string? UsuarioNome,
    DateTime CriadoEm);
