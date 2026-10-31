using Aurora.Api.Seguranca;
using Aurora.Application.Comum;
using Aurora.Application.Estoque;
using Aurora.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

[ApiController]
[Route("api/v1/estoque")]
[Authorize]
[Produces("application/json")]
public class EstoqueController(EstoqueService estoque) : ControllerBase
{
    // Os perfis ficam em cada ação: o [Authorize] de uma ação soma ao da classe (E, não OU), então um perfil
    // a mais, como o Operador no saldo de matéria-prima, não caberia num atributo de classe.

    /// <summary>
    /// Saldo das matérias-primas ativas por nome, com a marca de crítico (saldo no mínimo ou abaixo, RN08). O Operador
    /// também consulta, para saber se há material antes de iniciar uma etapa.
    /// </summary>
    [HttpGet("materias-primas")]
    [Authorize(Roles = Perfis.SaldoDeMateriaPrima)]
    public async Task<ActionResult<Pagina<SaldoDeMateriaPrimaDto>>> ListarSaldosDeMateriaPrima(
        [FromQuery] string? busca,
        [FromQuery] bool? critico,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await estoque.ListarSaldosDeMateriaPrimaAsync(busca, critico, page, pageSize, ct));

    /// <summary>Saldo de produto acabado: total, reservado a pedidos confirmados e disponível (RF14).</summary>
    [HttpGet("produtos")]
    [Authorize(Roles = Perfis.ConsultaDeEstoque)]
    public async Task<ActionResult<Pagina<SaldoDeProdutoDto>>> ListarSaldosDeProduto(
        [FromQuery] string? busca,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await estoque.ListarSaldosDeProdutoAsync(busca, page, pageSize, ct));

    /// <summary>Extrato do livro-razão de estoque, do mais recente ao mais antigo (RF14).</summary>
    [HttpGet("movimentos")]
    [Authorize(Roles = Perfis.ConsultaDeEstoque)]
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

    /// <summary>Alertas de matéria-prima no mínimo ou abaixo que ainda não foram lidos, o mais antigo primeiro (RF15).</summary>
    [HttpGet("alertas")]
    [Authorize(Roles = Perfis.ConsultaDeEstoque)]
    public async Task<ActionResult<Pagina<AlertaDeEstoqueDto>>> ListarAlertas(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await estoque.ListarAlertasAbertosAsync(page, pageSize, ct));

    /// <summary>Marca o alerta como lido. Repetir não muda quem leu nem quando.</summary>
    [HttpPost("alertas/{id:int}/ler")]
    [Authorize(Roles = Perfis.Almoxarife)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AlertaDeEstoqueDto>> MarcarAlertaComoLido(int id, CancellationToken ct) =>
        Ok(await estoque.MarcarAlertaComoLidoAsync(id, ct));
}
