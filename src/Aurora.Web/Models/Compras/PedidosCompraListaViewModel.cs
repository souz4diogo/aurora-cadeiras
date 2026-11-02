using Aurora.Web.Api.Compras;
using Aurora.Web.Api.Comum;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Models.Compras;

public sealed record PedidosCompraListaViewModel(
    Pagina<PedidoCompraResumoResposta> Pagina,
    FiltroDePedidosCompra Filtro,
    IReadOnlyList<SelectListItem> Fornecedores);
