using Aurora.Api.Seguranca;
using Aurora.Application.Roteiros;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

[ApiController]
[Route("api/v1/produtos/{produtoId:int}/roteiro")]
[Authorize(Roles = Perfis.Internos)]
[Produces("application/json")]
public class RoteiroController(RoteiroService roteiros) : ControllerBase
{
    /// <summary>Estações que o produto percorre, em ordem; definem as etapas das ordens de produção (RF10).</summary>
    [HttpGet]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RoteiroDto>> Obter(int produtoId, CancellationToken ct) =>
        Ok(await roteiros.ObterAsync(produtoId, ct));

    /// <summary>Substitui o roteiro: a ordem da lista de estações vira a sequência das etapas.</summary>
    [HttpPut]
    [Authorize(Roles = Perfis.AdministradorOuGerente)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RoteiroDto>> Substituir(int produtoId, RoteiroRequest request, CancellationToken ct) =>
        Ok(await roteiros.SubstituirAsync(produtoId, request, ct));
}
