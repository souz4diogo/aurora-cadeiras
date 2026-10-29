using Aurora.Api.Seguranca;
using Aurora.Application.Comum;
using Aurora.Application.Usuarios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize(Roles = Perfis.Administrador)]
[Produces("application/json")]
public class UsuariosController(UsuarioService usuarios) : ControllerBase
{
    /// <summary>Lista os perfis de acesso.</summary>
    [HttpGet("perfis")]
    public ActionResult<IReadOnlyList<PerfilDto>> ListarPerfis() => Ok(UsuarioService.ListarPerfis());

    /// <summary>Lista paginada de usuários (RF02).</summary>
    [HttpGet("usuarios")]
    public async Task<ActionResult<Pagina<UsuarioDto>>> Listar(
        [FromQuery] string? busca, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await usuarios.ListarAsync(busca, page, pageSize, ct));

    [HttpGet("usuarios/{id:int}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UsuarioDto>> Obter(int id, CancellationToken ct) =>
        Ok(await usuarios.ObterAsync(id, ct));

    /// <summary>Cria o usuário com senha provisória (mostrada somente nesta resposta).</summary>
    [HttpPost("usuarios")]
    [ProducesResponseType<UsuarioComSenhaProvisoriaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UsuarioComSenhaProvisoriaDto>> Criar(CriarUsuarioRequest request, CancellationToken ct)
    {
        var criado = await usuarios.CriarAsync(request, ct);
        return Created($"/api/v1/usuarios/{criado.Usuario.Id}", criado);
    }

    [HttpPut("usuarios/{id:int}")]
    public async Task<ActionResult<UsuarioDto>> Atualizar(int id, AtualizarUsuarioRequest request, CancellationToken ct) =>
        Ok(await usuarios.AtualizarAsync(id, request, ct));

    /// <summary>Ativa ou inativa o usuário (inativar revoga as sessões).</summary>
    [HttpPatch("usuarios/{id:int}/ativo")]
    public async Task<ActionResult<UsuarioDto>> AlterarAtivo(int id, AlterarAtivoRequest request, CancellationToken ct) =>
        Ok(await usuarios.AlterarAtivoAsync(id, request.Ativo, ct));

    /// <summary>Gera nova senha provisória e revoga as sessões do usuário.</summary>
    [HttpPost("usuarios/{id:int}/redefinir-senha")]
    public async Task<ActionResult<UsuarioComSenhaProvisoriaDto>> RedefinirSenha(int id, CancellationToken ct) =>
        Ok(await usuarios.RedefinirSenhaAsync(id, ct));
}
