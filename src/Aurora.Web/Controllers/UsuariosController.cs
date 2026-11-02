using System.Globalization;
using Aurora.Web.Api.Clientes;
using Aurora.Web.Api.Comum;
using Aurora.Web.Api.Usuarios;
using Aurora.Web.Extensions;
using Aurora.Web.Models.Usuarios;
using Aurora.Web.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Controllers;

/// <summary>Usuários e perfis (RF02): só o Administrador. A senha provisória aparece uma única vez, na resposta do POST.</summary>
[Authorize(Roles = Perfis.Administrador)]
public class UsuariosController(IUsuariosApi usuarios, IClientesApi clientes) : AuroraController
{
    [HttpGet]
    public async Task<IActionResult> Index(string? busca, int page = 1, CancellationToken ct = default) =>
        View(new UsuariosListaViewModel(await usuarios.ListarAsync(busca, page, ct), busca));

    [HttpGet]
    public async Task<IActionResult> Novo(CancellationToken ct) => await FormularioAsync(new UsuarioFormViewModel(), ct);

    [HttpPost]
    public async Task<IActionResult> Novo(UsuarioFormViewModel modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return await FormularioAsync(modelo, ct);
        }

        return await TentarAsync(
            async () =>
            {
                var criado = await usuarios.CriarAsync(modelo.ParaRequisicao(), ct);
                return MostrarSenha(criado, redefinicao: false);
            },
            () => FormularioAsync(modelo, ct));
    }

    [HttpGet]
    public async Task<IActionResult> Editar(int id, CancellationToken ct) =>
        await FormularioAsync(UsuarioFormViewModel.De(await usuarios.ObterAsync(id, ct)), ct);

    [HttpPost]
    public async Task<IActionResult> Editar(int id, UsuarioFormViewModel modelo, CancellationToken ct)
    {
        modelo.Id = id;
        if (!ModelState.IsValid)
        {
            return await FormularioAsync(modelo, ct);
        }

        return await TentarAsync(
            async () =>
            {
                var atualizado = await usuarios.AtualizarAsync(id, modelo.ParaRequisicao(), ct);
                AvisarSucesso($"Usuário {atualizado.Nome} atualizado.");
                return RedirectToAction(nameof(Index));
            },
            () => FormularioAsync(modelo, ct));
    }

    [HttpPost]
    public async Task<IActionResult> AlterarAtivo(int id, bool ativo, CancellationToken ct)
    {
        try
        {
            var usuario = await usuarios.AlterarAtivoAsync(id, ativo, ct);
            AvisarSucesso(ativo
                ? $"Usuário {usuario.Nome} ativado."
                : $"Usuário {usuario.Nome} inativado; as sessões dele foram encerradas.");
        }
        catch (ApiException erro) when (erro.EhFalhaDeNegocio)
        {
            AvisarAtencao(erro.Detalhe);
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>Tela de confirmação: redefinir encerra as sessões do usuário e a senha antiga deixa de valer.</summary>
    [HttpGet]
    public async Task<IActionResult> RedefinirSenha(int id, CancellationToken ct) => View(await usuarios.ObterAsync(id, ct));

    [HttpPost]
    [ActionName("RedefinirSenha")]
    public async Task<IActionResult> ConfirmarRedefinicao(int id, CancellationToken ct) =>
        await TentarAsync(
            async () => MostrarSenha(await usuarios.RedefinirSenhaAsync(id, ct), redefinicao: true),
            async () => View(await usuarios.ObterAsync(id, ct)));

    /// <summary>Mostra a senha provisória sem guardá-la em lugar nenhum (nem TempData) e sem permitir cache.</summary>
    private ViewResult MostrarSenha(UsuarioComSenhaProvisoriaResposta resultado, bool redefinicao)
    {
        Response.Headers.CacheControl = "no-store";
        return View("SenhaProvisoria", new SenhaProvisoriaViewModel(
            resultado.Usuario.Nome, resultado.Usuario.Email, resultado.SenhaProvisoria, redefinicao));
    }

    private async Task<IActionResult> FormularioAsync(UsuarioFormViewModel modelo, CancellationToken ct)
    {
        modelo.Perfis = (await usuarios.ListarPerfisAsync(ct))
            .Select(p => new SelectListItem(Formatacao.NomeDoPerfil(p.Nome), p.Id.ToString(CultureInfo.InvariantCulture)))
            .ToList();
        var opcoes = (await clientes.ListarAtivosAsync(ct))
            .Select(c => new SelectListItem(c.Nome, c.Id.ToString(CultureInfo.InvariantCulture)))
            .ToList();
        if (modelo.ClienteId is int vinculado && opcoes.All(o => o.Value != vinculado.ToString(CultureInfo.InvariantCulture)))
        {
            var atual = await clientes.ObterAsync(vinculado, ct);
            opcoes.Add(new SelectListItem($"{atual.Nome} (inativo)", vinculado.ToString(CultureInfo.InvariantCulture)));
        }

        modelo.Clientes = opcoes;
        return View("Form", modelo);
    }
}
