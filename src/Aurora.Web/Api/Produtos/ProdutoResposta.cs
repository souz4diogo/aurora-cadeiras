namespace Aurora.Web.Api.Produtos;

public sealed record ProdutoResposta(
    int Id,
    string Codigo,
    string Nome,
    byte LinhaProdutoId,
    string LinhaProduto,
    decimal PrecoVenda,
    bool Ativo,
    DateTime CriadoEm);
