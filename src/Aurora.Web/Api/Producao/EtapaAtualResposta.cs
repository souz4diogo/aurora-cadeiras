namespace Aurora.Web.Api.Producao;

/// <summary>Etapa em que a OP está agora (a primeira ainda não concluída).</summary>
public sealed record EtapaAtualResposta(
    int EtapaId,
    byte EstacaoId,
    string EstacaoNome,
    byte Sequencia,
    string Status,
    DateTime? IniciadaEm,
    int? MinutosNaEtapa);
