using Aurora.Api.Seguranca;
using Aurora.Application.Producao;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

[ApiController]
[Route("api/v1/apontamentos")]
[Authorize(Roles = Perfis.OperadorOuInspetor)]
[Produces("application/json")]
public class ApontamentosController(ApontamentoService apontamentos) : ControllerBase
{
    /// <summary>
    /// Sincroniza o lote de apontamentos feitos offline (RF23). Os itens são processados em ordem e de forma
    /// independente: o resultado de cada um é <c>Aplicado</c>, <c>JaProcessado</c> (o mesmo <c>clientRequestId</c>
    /// já foi aplicado, sem repetir o efeito) ou <c>Rejeitado</c> (com <c>code</c> e <c>mensagem</c>). Um item
    /// rejeitado responde 200 e não derruba os demais; só um lote malformado ou grande demais responde 400.
    /// </summary>
    [HttpPost("sincronizar")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SincronizacaoResponse>> Sincronizar(SincronizacaoRequest request, CancellationToken ct) =>
        Ok(await apontamentos.SincronizarAsync(request, ct));
}
