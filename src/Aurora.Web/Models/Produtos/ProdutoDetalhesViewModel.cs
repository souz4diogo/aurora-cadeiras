using Aurora.Web.Api.Produtos;

namespace Aurora.Web.Models.Produtos;

public sealed record ProdutoDetalhesViewModel(ProdutoResposta Produto, RoteiroResposta Roteiro, FichaTecnicaResposta Ficha);
