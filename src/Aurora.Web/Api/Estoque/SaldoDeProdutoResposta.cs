namespace Aurora.Web.Api.Estoque;

public sealed record SaldoDeProdutoResposta(
    int ProdutoId,
    string Codigo,
    string Nome,
    int Quantidade,
    int Reservado,
    int Disponivel);
