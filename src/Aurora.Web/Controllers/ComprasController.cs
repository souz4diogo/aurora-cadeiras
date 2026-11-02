using System.Globalization;
using Aurora.Web.Api.Compras;
using Aurora.Web.Api.Fornecedores;
using Aurora.Web.Api.MateriasPrimas;
using Aurora.Web.Extensions;
using Aurora.Web.Models.Compras;
using Aurora.Web.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Controllers;

/// <summary>Pedidos de compra de matéria-prima (RF16/RF17): o Almoxarife cria, emite, cancela e recebe; gerência consulta.</summary>
[Authorize(Roles = Perfis.ConsultaDeCompras)]
public class ComprasController(IComprasApi compras, IFornecedoresApi fornecedores, IMateriasPrimasApi materiasPrimas)
    : AuroraController
{
    [HttpGet]
    public async Task<IActionResult> Index(
        string? status, int? fornecedorId, DateOnly? de, DateOnly? ate, bool? atrasados, int page = 1, CancellationToken ct = default)
    {
        var filtro = new FiltroDePedidosCompra(string.IsNullOrWhiteSpace(status) ? null : status, fornecedorId, de, ate, atrasados);
        var opcoes = (await fornecedores.ListarAtivosAsync(ct)).Select(f => new SelectListItem(f.RazaoSocial, Identificador(f.Id))).ToList();
        return View(new PedidosCompraListaViewModel(await compras.ListarAsync(filtro, page, ct), filtro, opcoes));
    }

    [HttpGet]
    public async Task<IActionResult> Detalhes(int id, CancellationToken ct) => View(await compras.ObterAsync(id, ct));

    [HttpGet]
    [Authorize(Roles = Perfis.Almoxarife)]
    public async Task<IActionResult> Novo(int? materiaPrimaId, CancellationToken ct) =>
        await FormularioAsync(new PedidoCompraFormViewModel { Itens = [new CompraItemFormViewModel { MateriaPrimaId = materiaPrimaId }] }, ct);

    [HttpPost]
    [Authorize(Roles = Perfis.Almoxarife)]
    public async Task<IActionResult> Novo(PedidoCompraFormViewModel modelo, CancellationToken ct)
    {
        if (modelo.Itens.Count == 0)
        {
            ModelState.AddModelError(nameof(PedidoCompraFormViewModel.Itens), "Adicione ao menos um item ao pedido de compra.");
        }

        if (!ModelState.IsValid)
        {
            return await FormularioAsync(modelo, ct);
        }

        return await TentarAsync(
            async () =>
            {
                var criado = await compras.CriarAsync(modelo.ParaRequisicao(), ct);
                AvisarSucesso($"Pedido de compra {criado.Numero} criado. Emita-o para enviar ao fornecedor.");
                return RedirectToAction(nameof(Detalhes), new { id = criado.Id });
            },
            () => FormularioAsync(modelo, ct));
    }

    [HttpPost]
    [Authorize(Roles = Perfis.Almoxarife)]
    public async Task<IActionResult> Emitir(int id, CancellationToken ct) =>
        await TentarAsync(
            async () =>
            {
                var emitido = await compras.EmitirAsync(id, ct);
                AvisarSucesso($"Pedido de compra {emitido.Numero} emitido.");
                return RedirectToAction(nameof(Detalhes), new { id });
            },
            async () => View(nameof(Detalhes), await compras.ObterAsync(id, ct)));

    /// <summary>Tela de confirmação: cancelar não tem volta, então pede uma segunda confirmação.</summary>
    [HttpGet]
    [Authorize(Roles = Perfis.Almoxarife)]
    public async Task<IActionResult> Cancelar(int id, CancellationToken ct) => View(await compras.ObterAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Perfis.Almoxarife)]
    [ActionName("Cancelar")]
    public async Task<IActionResult> ConfirmarCancelamento(int id, CancellationToken ct) =>
        await TentarAsync(
            async () =>
            {
                var cancelado = await compras.CancelarAsync(id, ct);
                AvisarSucesso($"Pedido de compra {cancelado.Numero} cancelado.");
                return RedirectToAction(nameof(Detalhes), new { id });
            },
            async () => View(await compras.ObterAsync(id, ct)));

    /// <summary>Recebimento por item: a quantidade que chegou agora (pode ser parcial) entra no estoque na mesma transação.</summary>
    [HttpPost]
    [Authorize(Roles = Perfis.Almoxarife)]
    public async Task<IActionResult> Receber(int id, int itemId, decimal? quantidade, CancellationToken ct)
    {
        if (quantidade is not > 0)
        {
            ModelState.AddModelError("quantidade", "Informe a quantidade que chegou, maior que zero.");
            return View(nameof(Detalhes), await compras.ObterAsync(id, ct));
        }

        return await TentarAsync(
            async () =>
            {
                var pedido = await compras.ReceberItemAsync(itemId, quantidade.Value, ct);
                var item = pedido.Itens.First(i => i.Id == itemId);
                AvisarSucesso(
                    $"Recebido: {Formatacao.Quantidade(quantidade.Value)} {item.Unidade} de {item.MateriaPrimaNome}. " +
                    $"O estoque foi atualizado; o pedido está {Formatacao.StatusDaCompra(pedido.Status).ToLowerInvariant()}.");
                return RedirectToAction(nameof(Detalhes), new { id });
            },
            async () => View(nameof(Detalhes), await compras.ObterAsync(id, ct)));
    }

    private async Task<IActionResult> FormularioAsync(PedidoCompraFormViewModel modelo, CancellationToken ct)
    {
        modelo.Fornecedores = (await fornecedores.ListarAtivosAsync(ct))
            .Select(f => new SelectListItem(f.ComunidadeTradicional ? $"{f.RazaoSocial} (comunidade tradicional)" : f.RazaoSocial, Identificador(f.Id)))
            .ToList();
        modelo.MateriasPrimas = (await materiasPrimas.ListarAtivasAsync(ct))
            .Select(m => new SelectListItem($"{m.Codigo} - {m.Nome} ({m.Unidade}, custo {Formatacao.Moeda(m.CustoUnitario)})", Identificador(m.Id)))
            .ToList();
        return View("Form", modelo);
    }

    private static string Identificador(int valor) => valor.ToString(CultureInfo.InvariantCulture);
}
