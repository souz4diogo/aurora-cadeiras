namespace Aurora.Web.Api.Produtos;

public sealed record ProdutoRequisicao(string Codigo, string Nome, byte LinhaProdutoId, decimal PrecoVenda);
