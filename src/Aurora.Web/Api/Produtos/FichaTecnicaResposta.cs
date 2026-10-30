namespace Aurora.Web.Api.Produtos;

public sealed record FichaTecnicaResposta(int ProdutoId, IReadOnlyList<FichaTecnicaItemResposta> Itens);
