namespace Aurora.Web.Api.Produtos;

/// <summary>Ids das estações na ordem em que o produto as percorre; a posição na lista vira a sequência.</summary>
public sealed record RoteiroRequisicao(IReadOnlyList<byte> Estacoes);
