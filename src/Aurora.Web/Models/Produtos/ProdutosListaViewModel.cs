using Aurora.Web.Api.Comum;
using Aurora.Web.Api.Produtos;

namespace Aurora.Web.Models.Produtos;

public sealed record ProdutosListaViewModel(Pagina<ProdutoResposta> Pagina, string? Busca, bool? Ativo);
