using Aurora.Api.Seguranca;
using Aurora.Application.FichasTecnicas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

[ApiController]
[Route("api/v1/produtos/{produtoId:int}/ficha-tecnica")]
[Authorize(Roles = Perfis.Internos)]
[Produces("application/json")]
public class FichaTecnicaController(FichaTecnicaService fichas) : ControllerBase
{
    /// <summary>Ficha técnica do produto: materiais, estações em que são consumidos e quantidades (RF04).</summary>
    [HttpGet]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FichaTecnicaDto>> Obter(int produtoId, CancellationToken ct) =>
        Ok(await fichas.ObterAsync(produtoId, ct));

    /// <summary>Substitui a ficha técnica inteira. Cada material precisa ser consumido numa estação do roteiro.</summary>
    [HttpPut]
    [Authorize(Roles = Perfis.AdministradorOuGerente)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<FichaTecnicaDto>> Substituir(int produtoId, FichaTecnicaRequest request, CancellationToken ct) =>
        Ok(await fichas.SubstituirAsync(produtoId, request, ct));
}
