using Aurora.Web.Api.Clientes;
using Aurora.Web.Models.Clientes;
using Aurora.Web.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

[Authorize(Roles = Perfis.ConsultaDeClientes)]
public class ClientesController(IClientesApi clientes) : AuroraController
{
    [HttpGet]
    public async Task<IActionResult> Index(string? busca, bool? ativo, int page = 1, CancellationToken ct = default) =>
        View(new ClientesListaViewModel(await clientes.ListarAsync(busca, ativo, page, ct), busca, ativo));

    [HttpGet]
    [Authorize(Roles = Perfis.AdministradorOuVendedor)]
    public IActionResult Novo() => View("Form", new ClienteFormViewModel());

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuVendedor)]
    public async Task<IActionResult> Novo(ClienteFormViewModel modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", modelo);
        }

        return await TentarAsync(
            async () =>
            {
                var criado = await clientes.CriarAsync(modelo.ParaRequisicao(), ct);
                AvisarSucesso($"Cliente {criado.Nome} cadastrado.");
                return RedirectToAction(nameof(Index));
            },
            () => View("Form", modelo));
    }

    [HttpGet]
    [Authorize(Roles = Perfis.AdministradorOuVendedor)]
    public async Task<IActionResult> Editar(int id, CancellationToken ct) =>
        View("Form", ClienteFormViewModel.De(await clientes.ObterAsync(id, ct)));

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuVendedor)]
    public async Task<IActionResult> Editar(int id, ClienteFormViewModel modelo, CancellationToken ct)
    {
        modelo.Id = id;
        if (!ModelState.IsValid)
        {
            return View("Form", modelo);
        }

        return await TentarAsync(
            async () =>
            {
                var atualizado = await clientes.AtualizarAsync(id, modelo.ParaRequisicao(), ct);
                AvisarSucesso($"Cliente {atualizado.Nome} atualizado.");
                return RedirectToAction(nameof(Index));
            },
            () => View("Form", modelo));
    }

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuVendedor)]
    public async Task<IActionResult> AlterarAtivo(int id, bool ativo, CancellationToken ct)
    {
        var cliente = await clientes.AlterarAtivoAsync(id, ativo, ct);
        AvisarSucesso($"Cliente {cliente.Nome} {(ativo ? "ativado" : "inativado")}.");
        return RedirectToAction(nameof(Index));
    }
}
