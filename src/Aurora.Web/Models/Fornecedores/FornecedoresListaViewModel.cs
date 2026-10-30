using Aurora.Web.Api.Comum;
using Aurora.Web.Api.Fornecedores;

namespace Aurora.Web.Models.Fornecedores;

public sealed record FornecedoresListaViewModel(
    Pagina<FornecedorResposta> Pagina, string? Busca, bool? Ativo, bool? ComunidadeTradicional);
