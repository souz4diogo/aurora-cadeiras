using System.Globalization;
using Aurora.Web.Api.Auditoria;
using Aurora.Web.Api.Usuarios;
using Aurora.Web.Models.Auditoria;
using Aurora.Web.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Controllers;

/// <summary>Histórico de alterações (RNF10): quem mudou o quê e quando, com o antes e o depois. Só o Administrador.</summary>
[Authorize(Roles = Perfis.Administrador)]
public class AuditoriaController(IAuditoriaApi auditoria, IUsuariosApi usuarios) : AuroraController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? tabela, string? registro, int? usuarioId, DateOnly? de, DateOnly? ate, int page = 1, CancellationToken ct = default)
    {
        var filtro = new FiltroDeAuditoria(
            string.IsNullOrWhiteSpace(tabela) ? null : tabela, string.IsNullOrWhiteSpace(registro) ? null : registro.Trim(), usuarioId, de, ate);
        var tabelas = await auditoria.ListarTabelasAsync(ct);
        var opcoesDeUsuario = (await usuarios.ListarTodosAsync(ct))
            .OrderBy(u => u.Nome, StringComparer.CurrentCultureIgnoreCase)
            .Select(u => new SelectListItem(u.Nome, u.Id.ToString(CultureInfo.InvariantCulture)))
            .ToList();

        try
        {
            return View(new AuditoriaViewModel(await auditoria.ListarAsync(filtro, page, ct), filtro, tabelas, opcoesDeUsuario));
        }
        catch (Api.Comum.ApiException erro) when (erro.EhFalhaDeNegocio)
        {
            // Filtro recusado (ex.: período invertido): volta ao filtro limpo com o aviso da API
            AvisarAtencao(erro.Detalhe);
            return RedirectToAction(nameof(Index));
        }
    }
}
