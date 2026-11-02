using Aurora.Web.Api.Expedicoes;
using Aurora.Web.Api.Pedidos;
using Aurora.Web.Extensions;
using Aurora.Web.Models.Expedicao;
using Aurora.Web.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

/// <summary>Expedição (RF19): pedidos Prontos esperando para sair e o histórico do que já saiu. Só o Almoxarife expede.</summary>
[Authorize(Roles = Perfis.ConsultaDeExpedicoes)]
public class ExpedicaoController(IPedidosApi pedidos, IExpedicoesApi expedicoes) : AuroraController
{
    private static readonly FiltroDePedidos SomenteProntos = new("Pronto", null, null, null, null);

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default) =>
        View(new ProntosViewModel(await pedidos.ListarAsync(SomenteProntos, page, ct)));

    [HttpGet]
    public async Task<IActionResult> Historico(int page = 1, CancellationToken ct = default) =>
        View(new HistoricoDeExpedicoesViewModel(await expedicoes.ListarAsync(page, ct)));

    /// <summary>Tela de confirmação: expedir baixa o produto acabado e fecha o pedido, então pede transportadora e rastreio.</summary>
    [HttpGet]
    [Authorize(Roles = Perfis.Almoxarife)]
    public async Task<IActionResult> Expedir(int id, CancellationToken ct)
    {
        var pedido = await pedidos.ObterAsync(id, ct);
        if (pedido.Status != "Pronto")
        {
            AvisarAtencao($"O pedido {pedido.Numero} está {Formatacao.StatusDoPedido(pedido.Status).ToLowerInvariant()}: só pedido pronto pode ser expedido.");
            return RedirectToAction("Detalhes", "Pedidos", new { id });
        }

        return View(new ExpedirFormViewModel { Pedido = pedido });
    }

    [HttpPost]
    [Authorize(Roles = Perfis.Almoxarife)]
    public async Task<IActionResult> Expedir(int id, ExpedirFormViewModel modelo, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            modelo.Pedido = await pedidos.ObterAsync(id, ct);
            return View(modelo);
        }

        return await TentarAsync(
            async () =>
            {
                var expedicao = await expedicoes.ExpedirAsync(id, modelo.ParaRequisicao(), ct);
                AvisarSucesso($"Pedido {expedicao.PedidoNumero} expedido. O produto acabado saiu do estoque.");
                return RedirectToAction(nameof(Historico));
            },
            async () =>
            {
                modelo.Pedido = await pedidos.ObterAsync(id, ct);
                return View(modelo);
            });
    }
}
