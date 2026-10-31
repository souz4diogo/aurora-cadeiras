using System.Globalization;
using Aurora.Web.Api.Estoque;
using Aurora.Web.Api.MateriasPrimas;
using Aurora.Web.Extensions;
using Aurora.Web.Models.Estoque;
using Aurora.Web.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Controllers;

[Authorize(Roles = Perfis.ConsultaDeEstoque)]
public class EstoqueController(IEstoqueApi estoque, IMateriasPrimasApi materiasPrimas) : AuroraController
{
    /// <summary>Saldos de matéria-prima, com a marca de crítico (no mínimo ou abaixo).</summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? busca, bool? critico, int page = 1, CancellationToken ct = default) =>
        View(new SaldosViewModel(await estoque.ListarSaldosDeMateriaPrimaAsync(busca, critico, page, ct), busca, critico));

    /// <summary>Saldos de produto acabado: total, reservado a pedidos e disponível.</summary>
    [HttpGet]
    public async Task<IActionResult> Produtos(string? busca, int page = 1, CancellationToken ct = default) =>
        View(new SaldosDeProdutoViewModel(await estoque.ListarSaldosDeProdutoAsync(busca, page, ct), busca));

    [HttpGet]
    public async Task<IActionResult> Movimentos(
        int? materiaPrimaId, string? tipo, string? origem, DateOnly? de, DateOnly? ate, int page = 1, CancellationToken ct = default)
    {
        var filtro = new FiltroDeMovimentos(
            materiaPrimaId, string.IsNullOrWhiteSpace(tipo) ? null : tipo, string.IsNullOrWhiteSpace(origem) ? null : origem, de, ate);
        var materias = (await materiasPrimas.ListarAtivasAsync(ct))
            .Select(m => new SelectListItem($"{m.Codigo} - {m.Nome}", Identificador(m.Id)))
            .ToList();
        return View(new MovimentosViewModel(await estoque.ListarMovimentosAsync(filtro, page, ct), filtro, materias));
    }

    [HttpGet]
    public async Task<IActionResult> Alertas(int page = 1, CancellationToken ct = default) =>
        View(new AlertasViewModel(await estoque.ListarAlertasAsync(page, ct)));

    [HttpPost]
    [Authorize(Roles = Perfis.Almoxarife)]
    public async Task<IActionResult> LerAlerta(int id, CancellationToken ct)
    {
        var alerta = await estoque.MarcarAlertaComoLidoAsync(id, ct);
        AvisarSucesso($"Alerta de {alerta.MateriaPrimaNome} marcado como lido.");
        return RedirectToAction(nameof(Alertas));
    }

    /// <summary>Só o Almoxarife ajusta o saldo (docs/06 §3). O motivo é obrigatório e fica no livro-razão.</summary>
    [HttpGet]
    [Authorize(Roles = Perfis.Almoxarife)]
    public async Task<IActionResult> Ajustar(int? materiaPrimaId, CancellationToken ct) =>
        await FormularioAsync(new AjusteFormViewModel { MateriaPrimaId = materiaPrimaId }, ct);

    [HttpPost]
    [Authorize(Roles = Perfis.Almoxarife)]
    public async Task<IActionResult> Ajustar(AjusteFormViewModel modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return await FormularioAsync(modelo, ct);
        }

        return await TentarAsync(
            async () =>
            {
                var ajuste = await estoque.AjustarAsync(modelo.ParaRequisicao(), ct);
                var materia = ajuste.MateriaPrima;
                AvisarSucesso(ajuste.Diferenca == 0
                    ? $"{materia.Nome} já estava com {Formatacao.Quantidade(materia.EstoqueAtual)} {materia.Unidade}: nada foi alterado."
                    : $"{materia.Nome} ajustada de {Formatacao.Quantidade(ajuste.SaldoAnterior)} para {Formatacao.Quantidade(materia.EstoqueAtual)} {materia.Unidade} ({Formatacao.QuantidadeComSinal(ajuste.Diferenca)}).");
                return RedirectToAction(nameof(Index));
            },
            () => FormularioAsync(modelo, ct));
    }

    private async Task<IActionResult> FormularioAsync(AjusteFormViewModel modelo, CancellationToken ct)
    {
        modelo.MateriasPrimas = (await materiasPrimas.ListarAtivasAsync(ct))
            .Select(m => new SelectListItem(
                $"{m.Codigo} - {m.Nome} (saldo {Formatacao.Quantidade(m.EstoqueAtual)} {m.Unidade})", Identificador(m.Id)))
            .ToList();
        return View("Ajustar", modelo);
    }

    private static string Identificador(int valor) => valor.ToString(CultureInfo.InvariantCulture);
}
