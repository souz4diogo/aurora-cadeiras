using Aurora.Api.Seguranca;
using Aurora.Application.Auditoria;
using Aurora.Application.Comum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

/// <summary>Histórico de alterações de pedidos, ordens de produção, matérias-primas e usuários (RNF10). Só o Administrador.</summary>
[ApiController]
[Route("api/v1/auditoria")]
[Authorize(Roles = Perfis.Administrador)]
[Produces("application/json")]
public class AuditoriaController(AuditoriaService auditoria) : ControllerBase
{
    /// <summary>
    /// Histórico da mais recente à mais antiga. Cada linha traz quem alterou, quando e os campos que mudaram (antes e
    /// depois). Filtros: <c>tabela</c> (uma das de <c>/auditoria/tabelas</c>), <c>registro</c> (a chave), <c>usuarioId</c>,
    /// <c>de</c> e <c>ate</c>.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Pagina<AuditoriaDto>>> Listar(
        [FromQuery] string? tabela,
        [FromQuery] string? registro,
        [FromQuery] int? usuarioId,
        [FromQuery] DateOnly? de,
        [FromQuery] DateOnly? ate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await auditoria.ListarAsync(new FiltroDeAuditoria(tabela, registro, usuarioId, de, ate), page, pageSize, ct));

    /// <summary>As tabelas que têm auditoria, para montar o filtro.</summary>
    [HttpGet("tabelas")]
    public ActionResult<IReadOnlyList<string>> Tabelas() => Ok(AuditoriaService.TabelasAuditadas);
}
