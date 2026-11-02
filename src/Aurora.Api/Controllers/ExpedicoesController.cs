using Aurora.Api.Seguranca;
using Aurora.Application.Comum;
using Aurora.Application.Expedicoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class ExpedicoesController(ExpedicaoService expedicoes) : ControllerBase
{
    /// <summary>Expedições, da mais recente à mais antiga (RF19). O vendedor vê só as dos próprios pedidos.</summary>
    [HttpGet("expedicoes")]
    [Authorize(Roles = Perfis.ConsultaDeExpedicoes)]
    public async Task<ActionResult<Pagina<ExpedicaoDto>>> Listar(
        [FromQuery] DateOnly? de,
        [FromQuery] DateOnly? ate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await expedicoes.ListarAsync(de, ate, page, pageSize, ct));

    /// <summary>
    /// Expede o pedido (RF19). Só pedido Pronto sai (409 RN07): a reserva é liberada, o produto acabado é baixado do
    /// estoque e o pedido passa a Expedido. O corpo é opcional: transportadora e código de rastreio podem faltar.
    /// </summary>
    [HttpPost("pedidos/{id:int}/expedir")]
    [Authorize(Roles = Perfis.Almoxarife)]
    [ProducesResponseType<ExpedicaoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ExpedicaoDto>> Expedir(int id, ExpedirRequest? request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await expedicoes.ExpedirAsync(id, request ?? new ExpedirRequest(null, null), ct));
}
