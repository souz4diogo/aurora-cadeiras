using Aurora.Web.Api.Fornecedores;
using Aurora.Web.Models.Fornecedores;
using Aurora.Web.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

[Authorize(Roles = Perfis.ConsultaDeFornecedores)]
public class FornecedoresController(IFornecedoresApi fornecedores) : AuroraController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? busca, bool? ativo, bool? comunidadeTradicional, int page = 1, CancellationToken ct = default) =>
        View(new FornecedoresListaViewModel(
            await fornecedores.ListarAsync(busca, ativo, comunidadeTradicional, page, ct), busca, ativo, comunidadeTradicional));

    [HttpGet]
    [Authorize(Roles = Perfis.AdministradorOuAlmoxarife)]
    public IActionResult Novo() => View("Form", new FornecedorFormViewModel());

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuAlmoxarife)]
    public async Task<IActionResult> Novo(FornecedorFormViewModel modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", modelo);
        }

        return await TentarAsync(
            async () =>
            {
                var criado = await fornecedores.CriarAsync(modelo.ParaRequisicao(), ct);
                AvisarSucesso($"Fornecedor {criado.RazaoSocial} cadastrado.");
                return RedirectToAction(nameof(Index));
            },
            () => View("Form", modelo));
    }

    [HttpGet]
    [Authorize(Roles = Perfis.AdministradorOuAlmoxarife)]
    public async Task<IActionResult> Editar(int id, CancellationToken ct) =>
        View("Form", FornecedorFormViewModel.De(await fornecedores.ObterAsync(id, ct)));

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuAlmoxarife)]
    public async Task<IActionResult> Editar(int id, FornecedorFormViewModel modelo, CancellationToken ct)
    {
        modelo.Id = id;
        if (!ModelState.IsValid)
        {
            return View("Form", modelo);
        }

        return await TentarAsync(
            async () =>
            {
                var atualizado = await fornecedores.AtualizarAsync(id, modelo.ParaRequisicao(), ct);
                AvisarSucesso($"Fornecedor {atualizado.RazaoSocial} atualizado.");
                return RedirectToAction(nameof(Index));
            },
            () => View("Form", modelo));
    }

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuAlmoxarife)]
    public async Task<IActionResult> AlterarAtivo(int id, bool ativo, CancellationToken ct)
    {
        var fornecedor = await fornecedores.AlterarAtivoAsync(id, ativo, ct);
        AvisarSucesso($"Fornecedor {fornecedor.RazaoSocial} {(ativo ? "ativado" : "inativado")}.");
        return RedirectToAction(nameof(Index));
    }
}
