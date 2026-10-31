namespace Aurora.Web.Api.Producao;

public sealed record ApontamentoResposta(
    long Id,
    int EtapaId,
    string Tipo,
    int UsuarioId,
    string UsuarioNome,
    DateTime DataHoraCliente,
    DateTime DataHoraServidor);
