using Aurora.Api.Seguranca;
using Aurora.Application.Comum;
using Aurora.Application.Estoque;
using Aurora.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

[ApiController]
[Route("api/v1/estoque")]
[Authorize(Roles = Perfis.ConsultaDeEstoque)]
[Produces("application/json")]
public class EstoqueController(EstoqueService estoque) : ControllerBase
{
    /// <summary>Extrato do livro-razão de estoque, do mais recente ao mais antigo (RF14).</summary>
    [HttpGet("movimentos")]
    public async Task<ActionResult<Pagina<MovimentoEstoqueDto>>> ListarMovimentos(
        [FromQuery] int? materiaPrimaId,
        [FromQuery] int? produtoId,
        [FromQuery] TipoMovimento? tipo,
        [FromQuery] OrigemMovimento? origem,
        [FromQuery] DateOnly? de,
        [FromQuery] DateOnly? ate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await estoque.ListarMovimentosAsync(materiaPrimaId, produtoId, tipo, origem, de, ate, page, pageSize, ct));

    /// <summary>
    /// Ajusta o saldo de uma matéria-prima para o valor contado, com motivo obrigatório. O movimento anterior não
    /// muda: a diferença entra como um novo movimento de ajuste.
    /// </summary>
    [HttpPost("ajustes")]
    [Authorize(Roles = Perfis.Almoxarife)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AjusteDeEstoqueDto>> Ajustar(AjusteDeEstoqueRequest request, CancellationToken ct) =>
        Ok(await estoque.AjustarAsync(request, ct));
}
