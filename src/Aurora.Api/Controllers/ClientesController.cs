using Aurora.Api.Seguranca;
using Aurora.Application.Clientes;
using Aurora.Application.Comum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

[ApiController]
[Route("api/v1/clientes")]
[Authorize(Roles = Perfis.ConsultaDeClientes)]
[Produces("application/json")]
public class ClientesController(ClienteService clientes) : ControllerBase
{
    /// <summary>Lista paginada de clientes, com busca por nome ou CPF/CNPJ e filtro por ativo (RF06).</summary>
    [HttpGet]
    public async Task<ActionResult<Pagina<ClienteDto>>> Listar(
        [FromQuery] string? busca,
        [FromQuery] bool? ativo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        Ok(await clientes.ListarAsync(busca, ativo, page, pageSize, ct));

    [HttpGet("{id:int}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClienteDto>> Obter(int id, CancellationToken ct) =>
        Ok(await clientes.ObterAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuVendedor)]
    [ProducesResponseType<ClienteDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClienteDto>> Criar(ClienteRequest request, CancellationToken ct)
    {
        var criado = await clientes.CriarAsync(request, ct);
        return CreatedAtAction(nameof(Obter), new { id = criado.Id }, criado);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Perfis.AdministradorOuVendedor)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClienteDto>> Atualizar(int id, ClienteRequest request, CancellationToken ct) =>
        Ok(await clientes.AtualizarAsync(id, request, ct));

    /// <summary>Ativa ou inativa o cliente.</summary>
    [HttpPatch("{id:int}/ativo")]
    [Authorize(Roles = Perfis.AdministradorOuVendedor)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClienteDto>> AlterarAtivo(int id, AlterarAtivoRequest request, CancellationToken ct) =>
        Ok(await clientes.AlterarAtivoAsync(id, request.Ativo, ct));
}
