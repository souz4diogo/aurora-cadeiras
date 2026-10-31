using Aurora.Api.Seguranca;
using Aurora.Application.Comum;
using Aurora.Application.MateriasPrimas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

[ApiController]
[Route("api/v1/materias-primas")]
[Authorize(Roles = Perfis.ConsultaDeMateriaPrima)]
[Produces("application/json")]
public class MateriasPrimasController(MateriaPrimaService materiasPrimas) : ControllerBase
{
    /// <summary>Lista paginada de matérias-primas, com busca por código ou nome e filtro por ativo (RF05).</summary>
    [HttpGet]
    public async Task<ActionResult<Pagina<MateriaPrimaDto>>> Listar(
        [FromQuery] string? busca,
        [FromQuery] bool? ativo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await materiasPrimas.ListarAsync(busca, ativo, page, pageSize, ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MateriaPrimaDto>> Obter(int id, CancellationToken ct) =>
        Ok(await materiasPrimas.ObterAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuAlmoxarife)]
    [ProducesResponseType<MateriaPrimaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MateriaPrimaDto>> Criar(MateriaPrimaRequest request, CancellationToken ct)
    {
        var criada = await materiasPrimas.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id = criada.Id }, criada);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Perfis.AdministradorOuAlmoxarife)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MateriaPrimaDto>> Atualizar(int id, MateriaPrimaRequest request, CancellationToken ct) =>
        Ok(await materiasPrimas.AtualizarAsync(id, request, ct));

    /// <summary>Ativa ou inativa a matéria-prima.</summary>
    [HttpPatch("{id:int}/ativo")]
    [Authorize(Roles = Perfis.AdministradorOuAlmoxarife)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MateriaPrimaDto>> AlterarAtivo(int id, AlterarAtivoRequest request, CancellationToken ct) =>
        Ok(await materiasPrimas.AlterarAtivoAsync(id, request.Ativo, ct));
}
