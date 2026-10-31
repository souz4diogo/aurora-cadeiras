using Aurora.Web.Api.Estacoes;
using Aurora.Web.Api.Producao;
using Aurora.Web.Extensions;
using Aurora.Web.Models.Producao;
using Aurora.Web.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

[Authorize(Roles = Perfis.ConsultaDeProducao)]
public class ProducaoController(IProducaoApi producao, IEstacoesApi estacoes) : AuroraController
{
    /// <summary>Quadro de produção: uma coluna por estação, com as OPs que estão nela agora.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await producao.ObterQuadroAsync(ct));

    [HttpGet]
    public async Task<IActionResult> Ordens(
        string? status, byte? estacaoId, int? pedidoId, bool? atrasadas, int page = 1, CancellationToken ct = default)
    {
        var filtro = new FiltroDeOrdens(string.IsNullOrWhiteSpace(status) ? null : status, estacaoId, pedidoId, atrasadas);
        var lista = await producao.ListarOrdensAsync(filtro, page, ct);
        var comEtapa = (await estacoes.ListarAsync(ct)).Where(e => e.GeraEtapa).ToList();
        return View(new OrdensListaViewModel(lista, filtro, comEtapa));
    }

    [HttpGet]
    public async Task<IActionResult> Detalhes(int id, CancellationToken ct) => View(await producao.ObterOrdemAsync(id, ct));

    /// <summary>Página de impressão da etiqueta: o QR carrega o código da OP, que o app mobile lê para abrir a OP.</summary>
    [HttpGet]
    public async Task<IActionResult> Etiqueta(int id, CancellationToken ct)
    {
        var ordem = await producao.ObterOrdemAsync(id, ct);
        var qr = CodigoQr.Svg(ordem.CodigoQr.ToString("D"), $"QR code da ordem de produção {ordem.Ordem.Numero}");
        return View(new EtiquetaViewModel(ordem, qr));
    }
}
