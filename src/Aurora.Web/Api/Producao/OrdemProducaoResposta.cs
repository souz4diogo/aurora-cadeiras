namespace Aurora.Web.Api.Producao;

/// <summary>OP com etapas, apontamentos, perdas e o código que vai no QR da etiqueta.</summary>
public sealed record OrdemProducaoResposta(
    OrdemProducaoResumoResposta Ordem,
    Guid CodigoQr,
    string ClienteNome,
    IReadOnlyList<EtapaResposta> Etapas,
    IReadOnlyList<ApontamentoResposta> Apontamentos,
    IReadOnlyList<PerdaResposta> Perdas);
