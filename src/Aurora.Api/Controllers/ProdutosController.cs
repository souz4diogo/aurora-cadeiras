using Aurora.Api.Seguranca;
using Aurora.Application.Comum;
using Aurora.Application.Produtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

[ApiController]
[Route("api/v1/produtos")]
[Authorize(Roles = Perfis.Internos)]
[Produces("application/json")]
public class ProdutosController(ProdutoService produtos) : ControllerBase
{
    /// <summary>Lista paginada de produtos, com busca por código ou nome e filtro por ativo (RF03).</summary>
    [HttpGet]
    public async Task<ActionResult<Pagina<ProdutoDto>>> Listar(
        [FromQuery] string? busca,
        [FromQuery] bool? ativo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await produtos.ListarAsync(busca, ativo, page, pageSize, ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProdutoDto>> Obter(int id, CancellationToken ct) =>
        Ok(await produtos.ObterAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuGerente)]
    [ProducesResponseType<ProdutoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProdutoDto>> Criar(ProdutoRequest request, CancellationToken ct)
    {
        var criado = await produtos.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id = criado.Id }, criado);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Perfis.AdministradorOuGerente)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProdutoDto>> Atualizar(int id, ProdutoRequest request, CancellationToken ct) =>
        Ok(await produtos.AtualizarAsync(id, request, ct));

    /// <summary>Ativa ou inativa o produto.</summary>
    [HttpPatch("{id:int}/ativo")]
    [Authorize(Roles = Perfis.AdministradorOuGerente)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProdutoDto>> AlterarAtivo(int id, AlterarAtivoRequest request, CancellationToken ct) =>
        Ok(await produtos.AlterarAtivoAsync(id, request.Ativo, ct));
}
