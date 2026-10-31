namespace Aurora.Web.Api.Produtos;

public sealed record RoteiroResposta(int ProdutoId, IReadOnlyList<RoteiroEtapaResposta> Etapas);
