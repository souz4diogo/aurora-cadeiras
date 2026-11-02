using Aurora.Api.Seguranca;
using Aurora.Application.Comum;
using Aurora.Application.Painel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

/// <summary>
/// Painel gerencial (RF20). Cada ação declara os próprios perfis (e não a classe) porque o [Authorize] do controller
/// se soma ao da ação: aqui o vendedor entra só em "pedidos atrasados".
/// </summary>
[ApiController]
[Route("api/v1/painel")]
[Produces("application/json")]
public class PainelController(PainelService painel) : ControllerBase
{
    /// <summary>Cartões do painel: pedidos em aberto e atrasados, OPs, materiais críticos e compras a receber.</summary>
    [HttpGet("resumo")]
    [Authorize(Roles = Perfis.ConsultaDoPainel)]
    public async Task<ActionResult<ResumoDoPainelDto>> Resumo(CancellationToken ct) =>
        Ok(await painel.ObterResumoAsync(ct));

    /// <summary>
    /// Todas as estações que geram etapa, com as OPs que estão em cada uma agora, quantas atrasadas e o tempo médio
    /// das etapas em andamento. Estações sem OP vêm com zeros.
    /// </summary>
    [HttpGet("producao-por-estacao")]
    [Authorize(Roles = Perfis.ConsultaDoPainel)]
    public async Task<ActionResult<IReadOnlyList<ProducaoDaEstacaoDto>>> ProducaoPorEstacao(CancellationToken ct) =>
        Ok(await painel.ObterProducaoPorEstacaoAsync(ct));

    /// <summary>Pedidos em andamento com o prazo vencido, do mais atrasado ao menos. O vendedor vê só os próprios.</summary>
    [HttpGet("pedidos-atrasados")]
    [Authorize(Roles = Perfis.PedidosAtrasadosDoPainel)]
    public async Task<ActionResult<Pagina<PedidoAtrasadoDto>>> PedidosAtrasados(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await painel.ListarPedidosAtrasadosAsync(page, pageSize, ct));

    /// <summary>
    /// Os materiais que mais saíram para a produção nos últimos <c>dias</c> (padrão 30, de 1 a 365), para o gráfico de
    /// consumo.
    /// </summary>
    [HttpGet("consumo-de-materiais")]
    [Authorize(Roles = Perfis.ConsultaDoPainel)]
    public async Task<ActionResult<IReadOnlyList<ConsumoDeMaterialDto>>> ConsumoDeMateriais(
        [FromQuery] int? dias, CancellationToken ct) =>
        Ok(await painel.ObterConsumoAsync(dias, ct));
}
