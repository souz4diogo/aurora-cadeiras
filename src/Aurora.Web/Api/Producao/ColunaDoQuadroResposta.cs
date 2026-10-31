namespace Aurora.Web.Api.Producao;

public sealed record ColunaDoQuadroResposta(
    byte EstacaoId,
    string EstacaoNome,
    byte Ordem,
    IReadOnlyList<OrdemProducaoResumoResposta> Ordens);
