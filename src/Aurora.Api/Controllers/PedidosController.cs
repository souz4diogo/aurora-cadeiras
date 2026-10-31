using Aurora.Api.Seguranca;
using Aurora.Application.Comum;
using Aurora.Application.Pedidos;
using Aurora.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

[ApiController]
[Route("api/v1/pedidos")]
[Authorize(Roles = Perfis.ConsultaDePedidos)]
[Produces("application/json")]
public class PedidosController(PedidoService pedidos) : ControllerBase
{
    /// <summary>
    /// Lista paginada de pedidos, do mais recente ao mais antigo, com filtros por status, cliente, período do pedido
    /// e atraso (RF08). O vendedor vê apenas os próprios pedidos.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<Pagina<PedidoResumoDto>>> Listar(
        [FromQuery] StatusPedido? status,
        [FromQuery] int? clienteId,
        [FromQuery] DateOnly? de,
        [FromQuery] DateOnly? ate,
        [FromQuery] bool? atrasados,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await pedidos.ListarAsync(status, clienteId, de, ate, atrasados, page, pageSize, ct));

    /// <summary>Detalhe do pedido com os itens e o valor total.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PedidoDto>> Obter(int id, CancellationToken ct) =>
        Ok(await pedidos.ObterAsync(id, ct));

    /// <summary>Cria o pedido em Aberto; o preço de cada item é o preço de venda atual do produto (RF07).</summary>
    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuVendedor)]
    [ProducesResponseType<PedidoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PedidoDto>> Criar(PedidoRequest request, CancellationToken ct)
    {
        var criado = await pedidos.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id = criado.Id }, criado);
    }

    /// <summary>Edita o pedido (somente em Aberto); os itens informados substituem os atuais.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Perfis.AdministradorOuVendedor)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PedidoDto>> Atualizar(int id, PedidoRequest request, CancellationToken ct) =>
        Ok(await pedidos.AtualizarAsync(id, request, ct));

    /// <summary>
    /// Confirma o pedido (RN01): reserva o produto acabado disponível (RN02) e gera as ordens de produção do que
    /// falta. O pedido vira Em produção, ou Pronto se o estoque atendeu tudo.
    /// </summary>
    [HttpPost("{id:int}/confirmar")]
    [Authorize(Roles = Perfis.AdministradorOuVendedor)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PedidoDto>> Confirmar(int id, CancellationToken ct) =>
        Ok(await pedidos.ConfirmarAsync(id, ct));

    /// <summary>Cancela o pedido (Aberto, Confirmado ou Em produção), liberando reservas e cancelando OPs.</summary>
    [HttpPost("{id:int}/cancelar")]
    [Authorize(Roles = Perfis.AdministradorOuVendedor)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PedidoDto>> Cancelar(int id, CancellationToken ct) =>
        Ok(await pedidos.CancelarAsync(id, ct));
}
