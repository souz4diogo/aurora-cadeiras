using Aurora.Api.Seguranca;
using Aurora.Application.Producao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

[ApiController]
[Route("api/v1/etapas")]
[Authorize(Roles = Perfis.OperadorOuInspetor)]
[Produces("application/json")]
public class EtapasController(EtapaService etapas) : ControllerBase
{
    /// <summary>
    /// Inicia a etapa e baixa do estoque a matéria-prima da ficha técnica da estação (RN03). Sem saldo, 409 RN04;
    /// com a etapa anterior em aberto, 409 RN05. Reenviar o mesmo <c>clientRequestId</c> não baixa o estoque de novo.
    /// </summary>
    [HttpPost("{id:int}/iniciar")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EtapaDto>> Iniciar(int id, ApontamentoRequest request, CancellationToken ct) =>
        Ok(await etapas.IniciarAsync(id, request, ct));

    /// <summary>Finaliza a etapa em andamento; a de Qualidade só termina por inspeção.</summary>
    [HttpPost("{id:int}/finalizar")]
    [Authorize(Roles = Perfis.Operador)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EtapaDto>> Finalizar(int id, ApontamentoRequest request, CancellationToken ct) =>
        Ok(await etapas.FinalizarAsync(id, request, ct));

    /// <summary>Registra refugo ou retrabalho numa etapa em andamento (RF12).</summary>
    [HttpPost("{id:int}/perdas")]
    [Authorize(Roles = Perfis.Operador)]
    [ProducesResponseType<PerdaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PerdaDto>> RegistrarPerda(int id, PerdaRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await etapas.RegistrarPerdaAsync(id, request, ct));
}
