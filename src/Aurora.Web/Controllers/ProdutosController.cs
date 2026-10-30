using System.Globalization;
using Aurora.Web.Api.Estacoes;
using Aurora.Web.Api.MateriasPrimas;
using Aurora.Web.Api.Produtos;
using Aurora.Web.Models.Produtos;
using Aurora.Web.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Controllers;

[Authorize(Roles = Perfis.ConsultaDeProdutos)]
public class ProdutosController(IProdutosApi produtos, IMateriasPrimasApi materiasPrimas, IEstacoesApi estacoes) : AuroraController
{
    [HttpGet]
    public async Task<IActionResult> Index(string? busca, bool? ativo, int page = 1, CancellationToken ct = default) =>
        View(new ProdutosListaViewModel(await produtos.ListarAsync(busca, ativo, page, ct), busca, ativo));

    [HttpGet]
    public async Task<IActionResult> Detalhes(int id, CancellationToken ct) =>
        View(new ProdutoDetalhesViewModel(
            await produtos.ObterAsync(id, ct), await produtos.ObterRoteiroAsync(id, ct), await produtos.ObterFichaTecnicaAsync(id, ct)));

    [HttpGet]
    [Authorize(Roles = Perfis.AdministradorOuGerente)]
    public IActionResult Novo() => View("Form", new ProdutoFormViewModel());

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuGerente)]
    public async Task<IActionResult> Novo(ProdutoFormViewModel modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View("Form", modelo);
        }

        return await TentarAsync(
            async () =>
            {
                var criado = await produtos.CriarAsync(modelo.ParaRequisicao(), ct);
                AvisarSucesso($"Produto {criado.Nome} cadastrado. Defina o roteiro e a ficha técnica.");
                return RedirectToAction(nameof(Detalhes), new { id = criado.Id });
            },
            () => View("Form", modelo));
    }

    [HttpGet]
    [Authorize(Roles = Perfis.AdministradorOuGerente)]
    public async Task<IActionResult> Editar(int id, CancellationToken ct) =>
        View("Form", ProdutoFormViewModel.De(await produtos.ObterAsync(id, ct)));

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuGerente)]
    public async Task<IActionResult> Editar(int id, ProdutoFormViewModel modelo, CancellationToken ct)
    {
        modelo.Id = id;
        if (!ModelState.IsValid)
        {
            return View("Form", modelo);
        }

        return await TentarAsync(
            async () =>
            {
                var atualizado = await produtos.AtualizarAsync(id, modelo.ParaRequisicao(), ct);
                AvisarSucesso($"Produto {atualizado.Nome} atualizado.");
                return RedirectToAction(nameof(Index));
            },
            () => View("Form", modelo));
    }

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuGerente)]
    public async Task<IActionResult> AlterarAtivo(int id, bool ativo, CancellationToken ct)
    {
        var produto = await produtos.AlterarAtivoAsync(id, ativo, ct);
        AvisarSucesso($"Produto {produto.Nome} {(ativo ? "ativado" : "inativado")}.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Roles = Perfis.AdministradorOuGerente)]
    public async Task<IActionResult> Roteiro(int id, CancellationToken ct) =>
        View(RoteiroEditorViewModel.De(await produtos.ObterAsync(id, ct), await produtos.ObterRoteiroAsync(id, ct), await estacoes.ListarAsync(ct)));

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuGerente)]
    public async Task<IActionResult> Roteiro(int id, RoteiroEditorViewModel modelo, CancellationToken ct)
    {
        modelo.ProdutoId = id;
        var requisicao = modelo.ParaRequisicao();
        if (requisicao.Estacoes.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Marque ao menos uma estação para o roteiro.");
        }

        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        return await TentarAsync(
            async () =>
            {
                await produtos.SubstituirRoteiroAsync(id, requisicao, ct);
                AvisarSucesso("Roteiro atualizado.");
                return RedirectToAction(nameof(Detalhes), new { id });
            },
            () => View(modelo));
    }

    [HttpGet]
    [Authorize(Roles = Perfis.AdministradorOuGerente)]
    public async Task<IActionResult> FichaTecnica(int id, CancellationToken ct)
    {
        var modelo = FichaTecnicaEditorViewModel.De(await produtos.ObterAsync(id, ct), await produtos.ObterFichaTecnicaAsync(id, ct));
        return await EditorDaFichaAsync(modelo, ct);
    }

    [HttpPost]
    [Authorize(Roles = Perfis.AdministradorOuGerente)]
    public async Task<IActionResult> FichaTecnica(int id, FichaTecnicaEditorViewModel modelo, CancellationToken ct)
    {
        modelo.ProdutoId = id;
        if (!ModelState.IsValid)
        {
            return await EditorDaFichaAsync(modelo, ct);
        }

        return await TentarAsync(
            async () =>
            {
                await produtos.SubstituirFichaTecnicaAsync(id, modelo.ParaRequisicao(), ct);
                AvisarSucesso("Ficha técnica atualizada.");
                return RedirectToAction(nameof(Detalhes), new { id });
            },
            () => EditorDaFichaAsync(modelo, ct));
    }

    /// <summary>Monta o editor da ficha com as matérias-primas ativas e as estações do roteiro do produto.</summary>
    private async Task<IActionResult> EditorDaFichaAsync(FichaTecnicaEditorViewModel modelo, CancellationToken ct)
    {
        var roteiro = await produtos.ObterRoteiroAsync(modelo.ProdutoId, ct);
        var materias = await materiasPrimas.ListarAtivasAsync(ct);
        modelo.Materias = materias
            .Select(m => new SelectListItem($"{m.Codigo} - {m.Nome} ({m.Unidade})", m.Id.ToString(CultureInfo.InvariantCulture)))
            .ToList();
        modelo.Estacoes = roteiro.Etapas
            .Select(e => new SelectListItem(e.Estacao, e.EstacaoId.ToString(CultureInfo.InvariantCulture)))
            .ToList();
        return View("FichaTecnica", modelo);
    }
}
