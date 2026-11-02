using Aurora.Api.Seguranca;
using Aurora.Application.Comum;
using Aurora.Application.Pedidos;
using Aurora.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

/// <summary>
/// Acompanhamento de pedidos (RF22). Fica à parte do <see cref="PedidosController"/> porque aqui o perfil Cliente
/// entra, e o [Authorize] do controller de pedidos, somado ao da ação, o barraria.
/// </summary>
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class AcompanhamentoController(PedidoService pedidos) : ControllerBase
{
    /// <summary>Pedidos do cliente logado, do mais recente ao mais antigo, com filtro por status (RF22).</summary>
    [HttpGet("meus-pedidos")]
    [Authorize(Roles = Perfis.Cliente)]
    public async Task<ActionResult<Pagina<PedidoResumoDto>>> ListarMeus(
        [FromQuery] StatusPedido? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await pedidos.ListarMeusAsync(status, page, pageSize, ct));

    /// <summary>
    /// Pedido com os itens e a linha do tempo da produção (RF08, RF22). Vendedor e cliente só acompanham os próprios
    /// pedidos; os de outros respondem 404.
    /// </summary>
    [HttpGet("pedidos/{id:int}/acompanhamento")]
    [Authorize(Roles = Perfis.AcompanhamentoDePedido)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AcompanhamentoDto>> Obter(int id, CancellationToken ct) =>
        Ok(await pedidos.ObterAcompanhamentoAsync(id, ct));
}
