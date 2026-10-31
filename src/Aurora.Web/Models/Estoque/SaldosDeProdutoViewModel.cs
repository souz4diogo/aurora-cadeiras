using Aurora.Web.Api.Comum;
using Aurora.Web.Api.Estoque;

namespace Aurora.Web.Models.Estoque;

public sealed record SaldosDeProdutoViewModel(Pagina<SaldoDeProdutoResposta> Pagina, string? Busca);
