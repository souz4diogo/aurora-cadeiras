using Aurora.Api.Seguranca;
using Aurora.Application.Comum;
using Aurora.Application.Qualidade;
using Aurora.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public class QualidadeController(InspecaoService inspecoes) : ControllerBase
{
    /// <summary>
    /// Registra a inspeção da OP (RF18). Aprovada: conclui a OP, dá entrada do produto acabado e, se foi a última OP
    /// do pedido, deixa o pedido Pronto. Reprovada: exige <c>estacaoRetrabalhoId</c> (422 RN06) e reabre as etapas a
    /// partir dela. A OP precisa estar com a etapa de Qualidade em andamento (409).
    /// </summary>
    [HttpPost("ordens-producao/{id:int}/inspecoes")]
    [Authorize(Roles = Perfis.Inspetor)]
    [ProducesResponseType<InspecaoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<InspecaoDto>> Registrar(int id, InspecaoRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await inspecoes.RegistrarAsync(id, request, ct));

    /// <summary>Histórico de inspeções, da mais recente à mais antiga, com filtros por OP, resultado e período (RF18).</summary>
    [HttpGet("inspecoes")]
    [Authorize(Roles = Perfis.ConsultaDeInspecoes)]
    public async Task<ActionResult<Pagina<InspecaoDto>>> Listar(
        [FromQuery] int? ordemProducaoId,
        [FromQuery] ResultadoInspecao? resultado,
        [FromQuery] DateOnly? de,
        [FromQuery] DateOnly? ate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await inspecoes.ListarAsync(ordemProducaoId, resultado, de, ate, page, pageSize, ct));
}
