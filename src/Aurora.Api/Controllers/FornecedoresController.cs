using Aurora.Api.Seguranca;
using Aurora.Application.Comum;
using Aurora.Application.Fornecedores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

[ApiController]
[Route("api/v1/fornecedores")]
[Authorize(Roles = Perfis.ConsultaDeFornecedores)]
[Produces("application/json")]
public class FornecedoresController(FornecedorService fornecedores) : ControllerBase
{
    /// <summary>
    /// Lista paginada de fornecedores, com busca por razão social ou CNPJ e filtros por ativo e por
    /// comunidade tradicional (RF06).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<Pagina<FornecedorDto>>> Listar(
        [FromQuery] string? busca,
        [FromQuery] bool? ativo,
        [FromQuery] bool? comunidadeTradicional,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await fornecedores.ListarAsync(busca, ativo, comunidadeTradicional, page, pageSize, ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FornecedorDto>> Obter(int id, CancellationToken ct) =>
        Ok(await fornecedores.ObterAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuAlmoxarife)]
    [ProducesResponseType<FornecedorDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FornecedorDto>> Criar(FornecedorRequest request, CancellationToken ct)
    {
        var criado = await fornecedores.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id = criado.Id }, criado);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Perfis.AdministradorOuAlmoxarife)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FornecedorDto>> Atualizar(int id, FornecedorRequest request, CancellationToken ct) =>
        Ok(await fornecedores.AtualizarAsync(id, request, ct));

    /// <summary>Ativa ou inativa o fornecedor.</summary>
    [HttpPatch("{id:int}/ativo")]
    [Authorize(Roles = Perfis.AdministradorOuAlmoxarife)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FornecedorDto>> AlterarAtivo(int id, AlterarAtivoRequest request, CancellationToken ct) =>
        Ok(await fornecedores.AlterarAtivoAsync(id, request.Ativo, ct));
}
