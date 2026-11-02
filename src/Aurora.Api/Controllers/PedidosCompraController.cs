using Aurora.Api.Seguranca;
using Aurora.Application.Comum;
using Aurora.Application.Compras;
using Aurora.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

[ApiController]
[Route("api/v1/pedidos-compra")]
[Authorize(Roles = Perfis.ConsultaDeCompras)]
[Produces("application/json")]
public class PedidosCompraController(PedidoCompraService compras) : ControllerBase
{
    /// <summary>Lista paginada de pedidos de compra, do mais recente ao mais antigo, com filtros (RF16).</summary>
    [HttpGet]
    public async Task<ActionResult<Pagina<PedidoCompraResumoDto>>> Listar(
        [FromQuery] StatusPedidoCompra? status,
        [FromQuery] int? fornecedorId,
        [FromQuery] DateOnly? de,
        [FromQuery] DateOnly? ate,
        [FromQuery] bool? atrasados,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await compras.ListarAsync(status, fornecedorId, de, ate, atrasados, page, pageSize, ct));

    /// <summary>Detalhe do pedido com os itens, o que já chegou e o que ainda falta.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PedidoCompraDto>> Obter(int id, CancellationToken ct) =>
        Ok(await compras.ObterAsync(id, ct));

    /// <summary>
    /// Registra a solicitação de compra em <c>Solicitado</c>. Sem <c>custoUnitario</c> no item, vale o custo atual da
    /// matéria-prima. O comprador é o usuário logado.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = Perfis.Almoxarife)]
    [ProducesResponseType<PedidoCompraDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PedidoCompraDto>> Criar(PedidoCompraRequest request, CancellationToken ct)
    {
        var criado = await compras.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id = criado.Id }, criado);
    }

    /// <summary>Emite o pedido ao fornecedor (Solicitado → Emitido).</summary>
    [HttpPost("{id:int}/emitir")]
    [Authorize(Roles = Perfis.Almoxarife)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PedidoCompraDto>> Emitir(int id, CancellationToken ct) =>
        Ok(await compras.EmitirAsync(id, ct));

    /// <summary>Cancela o pedido enquanto nada foi recebido (409 depois do primeiro recebimento).</summary>
    [HttpPost("{id:int}/cancelar")]
    [Authorize(Roles = Perfis.Almoxarife)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PedidoCompraDto>> Cancelar(int id, CancellationToken ct) =>
        Ok(await compras.CancelarAsync(id, ct));

    /// <summary>
    /// Recebe uma quantidade de um item (RF17): dá entrada no estoque, atualiza o custo da matéria-prima e o status do
    /// pedido (parcial ou recebido). Acima do pedido, 422 <c>RECEBIMENTO_EXCEDENTE</c>; pedido não emitido, 409.
    /// </summary>
    [HttpPost("itens/{itemId:int}/receber")]
    [Authorize(Roles = Perfis.Almoxarife)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PedidoCompraDto>> ReceberItem(int itemId, ReceberItemRequest request, CancellationToken ct) =>
        Ok(await compras.ReceberItemAsync(itemId, request, ct));
}
