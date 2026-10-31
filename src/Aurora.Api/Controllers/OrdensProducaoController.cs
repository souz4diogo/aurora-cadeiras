using Aurora.Api.Seguranca;
using Aurora.Application.Comum;
using Aurora.Application.Producao;
using Aurora.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

[ApiController]
[Route("api/v1/ordens-producao")]
[Authorize(Roles = Perfis.DetalheDeProducao)]
[Produces("application/json")]
public class OrdensProducaoController(OrdemProducaoService ordens) : ControllerBase
{
    /// <summary>
    /// Lista paginada de OPs, da mais recente à mais antiga, com a etapa em que cada uma está (RF09). O filtro
    /// <c>estacaoId</c> pega as OPs que estão naquela estação agora. O vendedor vê só as OPs dos próprios pedidos.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = Perfis.ConsultaDeProducao)]
    public async Task<ActionResult<Pagina<OrdemProducaoResumoDto>>> Listar(
        [FromQuery] StatusOrdemProducao? status,
        [FromQuery] byte? estacaoId,
        [FromQuery] int? pedidoId,
        [FromQuery] bool? atrasadas,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await ordens.ListarAsync(status, estacaoId, pedidoId, atrasadas, page, pageSize, ct));

    /// <summary>Quadro de produção: as OPs em aberto agrupadas pela estação em que estão (RF20).</summary>
    [HttpGet("quadro")]
    [Authorize(Roles = Perfis.ConsultaDeProducao)]
    public async Task<ActionResult<QuadroDeProducaoDto>> ObterQuadro(CancellationToken ct) =>
        Ok(await ordens.ObterQuadroAsync(ct));

    /// <summary>Detalhe da OP com etapas, apontamentos, perdas e o código do QR (RF09 a RF12).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrdemProducaoDto>> Obter(int id, CancellationToken ct) =>
        Ok(await ordens.ObterAsync(id, ct));
}
