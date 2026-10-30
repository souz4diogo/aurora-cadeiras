namespace Aurora.Web.Api.Produtos;

/// <summary>A lista informada substitui a ficha técnica inteira do produto.</summary>
public sealed record FichaTecnicaRequisicao(IReadOnlyList<FichaTecnicaItemRequisicao> Itens);
