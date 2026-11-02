using Aurora.Web.Api.Painel;
using Aurora.Web.Models.Painel;
using Aurora.Web.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

/// <summary>Painel gerencial (RF20): o retrato de agora da fábrica, para Administrador e Gerente de Produção.</summary>
[Authorize(Roles = Perfis.AdministradorOuGerente)]
public class PainelController(IPainelApi painel) : AuroraController
{
    [HttpGet]
    public async Task<IActionResult> Index(int? dias, int page = 1, CancellationToken ct = default)
    {
        var janela = dias is int d && PainelViewModel.JanelasDeConsumo.Contains(d) ? d : PainelViewModel.JanelaPadrao;

        // Em sequência de propósito: a renovação do token é rotativa, e chamadas simultâneas com o token vencido
        // gastariam o mesmo refresh token duas vezes (a API trata como reuso e encerra a sessão)
        var resumo = await painel.ObterResumoAsync(ct);
        var estacoes = await painel.ObterProducaoPorEstacaoAsync(ct);
        var consumo = await painel.ObterConsumoAsync(janela, ct);
        var atrasados = await painel.ListarPedidosAtrasadosAsync(page, ct);

        return View(new PainelViewModel(resumo, estacoes, consumo, atrasados, janela));
    }
}
