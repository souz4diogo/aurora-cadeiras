using Aurora.Web.Api.Comum;
using Aurora.Web.Api.Pedidos;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Models.Pedidos;

/// <param name="Clientes">Clientes para o filtro; nulo quando o perfil não pode consultar clientes (o filtro some).</param>
public sealed record PedidosListaViewModel(
    Pagina<PedidoResumoResposta> Pagina,
    FiltroDePedidos Filtro,
    IReadOnlyList<SelectListItem>? Clientes);
