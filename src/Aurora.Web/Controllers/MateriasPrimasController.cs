using System.Globalization;
using Aurora.Web.Api.Fornecedores;
using Aurora.Web.Api.MateriasPrimas;
using Aurora.Web.Models.MateriasPrimas;
using Aurora.Web.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Controllers;

[Authorize(Roles = Perfis.ConsultaDeMateriasPrimas)]
public class MateriasPrimasController(IMateriasPrimasApi materiasPrimas, IFornecedoresApi fornecedores) : AuroraController
{
    [HttpGet]
    public async Task<IActionResult> Index(string? busca, bool? ativo, int page = 1, CancellationToken ct = default) =>
        View(new MateriasPrimasListaViewModel(await materiasPrimas.ListarAsync(busca, ativo, page, ct), busca, ativo));

    [HttpGet]
    [Authorize(Roles = Perfis.AdministradorOuAlmoxarife)]
    public async Task<IActionResult> Novo(CancellationToken ct) => await FormularioAsync(new MateriaPrimaFormViewModel(), ct);

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuAlmoxarife)]
    public async Task<IActionResult> Novo(MateriaPrimaFormViewModel modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return await FormularioAsync(modelo, ct);
        }

        return await TentarAsync(
            async () =>
            {
                var criada = await materiasPrimas.CriarAsync(modelo.ParaRequisicao(), ct);
                AvisarSucesso($"Matéria-prima {criada.Nome} cadastrada.");
                return RedirectToAction(nameof(Index));
            },
            () => FormularioAsync(modelo, ct));
    }

    [HttpGet]
    [Authorize(Roles = Perfis.AdministradorOuAlmoxarife)]
    public async Task<IActionResult> Editar(int id, CancellationToken ct) =>
        await FormularioAsync(MateriaPrimaFormViewModel.De(await materiasPrimas.ObterAsync(id, ct)), ct);

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuAlmoxarife)]
    public async Task<IActionResult> Editar(int id, MateriaPrimaFormViewModel modelo, CancellationToken ct)
    {
        modelo.Id = id;
        if (!ModelState.IsValid)
        {
            return await FormularioAsync(modelo, ct);
        }

        return await TentarAsync(
            async () =>
            {
                var atualizada = await materiasPrimas.AtualizarAsync(id, modelo.ParaRequisicao(), ct);
                AvisarSucesso($"Matéria-prima {atualizada.Nome} atualizada.");
                return RedirectToAction(nameof(Index));
            },
            () => FormularioAsync(modelo, ct));
    }

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuAlmoxarife)]
    public async Task<IActionResult> AlterarAtivo(int id, bool ativo, CancellationToken ct)
    {
        var materia = await materiasPrimas.AlterarAtivoAsync(id, ativo, ct);
        AvisarSucesso($"Matéria-prima {materia.Nome} {(ativo ? "ativada" : "inativada")}.");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Exibe o formulário com a lista de fornecedores ativos; o fornecedor já escolhido continua na lista mesmo se foi inativado.</summary>
    private async Task<IActionResult> FormularioAsync(MateriaPrimaFormViewModel modelo, CancellationToken ct)
    {
        var ativos = await fornecedores.ListarAtivosAsync(ct);
        var opcoes = ativos.Select(f => new SelectListItem(f.RazaoSocial, f.Id.ToString(CultureInfo.InvariantCulture))).ToList();
        if (modelo.FornecedorPreferencialId is int escolhido && ativos.All(f => f.Id != escolhido))
        {
            var atual = await fornecedores.ObterAsync(escolhido, ct);
            opcoes.Add(new SelectListItem($"{atual.RazaoSocial} (inativo)", escolhido.ToString(CultureInfo.InvariantCulture)));
        }

        modelo.Fornecedores = opcoes;
        return View("Form", modelo);
    }
}
