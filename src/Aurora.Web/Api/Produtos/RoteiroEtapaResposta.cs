namespace Aurora.Web.Api.Produtos;

public sealed record RoteiroEtapaResposta(byte Sequencia, byte EstacaoId, string Estacao, bool GeraEtapa);
