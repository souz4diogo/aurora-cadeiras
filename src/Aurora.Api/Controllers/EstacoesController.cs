using Aurora.Api.Seguranca;
using Aurora.Application.Estacoes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

[ApiController]
[Route("api/v1/estacoes")]
[Authorize(Roles = Perfis.Internos)]
[Produces("application/json")]
public class EstacoesController(EstacaoService estacoes) : ControllerBase
{
    /// <summary>Estações da linha de produção, na ordem do layout da fábrica (RF10).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EstacaoDto>>> Listar(CancellationToken ct) =>
        Ok(await estacoes.ListarAsync(ct));
}
