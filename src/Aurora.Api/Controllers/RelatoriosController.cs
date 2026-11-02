using Aurora.Api.Seguranca;
using Aurora.Application.Relatorios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

/// <summary>Relatórios exportáveis em PDF e Excel (RF21).</summary>
[ApiController]
[Route("api/v1/relatorios")]
[Authorize(Roles = Perfis.ConsultaDeRelatorios)]
[Produces("application/json")]
public class RelatoriosController(RelatorioService relatorios) : ControllerBase
{
    /// <summary>Os relatórios que o perfil logado pode gerar, com os filtros que cada um aceita.</summary>
    [HttpGet]
    public ActionResult<IReadOnlyList<RelatorioDisponivelDto>> Listar() => Ok(relatorios.ListarDisponiveis());

    /// <summary>
    /// Gera o relatório e devolve o arquivo para download. <c>tipo</c>: <c>pedidos</c>, <c>producao</c>,
    /// <c>estoque</c>, <c>consumo</c> ou <c>compras-comunitarias</c>. <c>formato</c>: <c>pdf</c> ou <c>xlsx</c>.
    /// Cada tipo usa os filtros que fazem sentido para ele (<c>de</c>/<c>ate</c>, <c>status</c>, <c>ano</c>).
    /// </summary>
    [HttpGet("{tipo}")]
    [Produces("application/pdf", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Gerar(
        string tipo,
        [FromQuery] string? formato,
        [FromQuery] DateOnly? de,
        [FromQuery] DateOnly? ate,
        [FromQuery] string? status,
        [FromQuery] int? ano,
        CancellationToken ct)
    {
        var arquivo = await relatorios.GerarAsync(tipo, formato, new FiltroDeRelatorio(de, ate, status, ano), ct);

        // Os relatórios trazem dados do negócio: nada de guardar em cache de navegador ou de proxy
        Response.Headers.CacheControl = "no-store";
        return File(arquivo.Conteudo, arquivo.TipoDeConteudo, arquivo.NomeDoArquivo);
    }
}
