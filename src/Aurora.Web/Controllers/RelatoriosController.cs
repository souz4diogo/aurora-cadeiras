using Aurora.Web.Api.Comum;
using Aurora.Web.Api.Relatorios;
using Aurora.Web.Models.Relatorios;
using Aurora.Web.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

/// <summary>Relatórios em PDF e Excel (RF21). A API gera e decide o que cada perfil pode pedir; aqui só se escolhe e se baixa.</summary>
[Authorize(Roles = Perfis.ConsultaDeRelatorios)]
public class RelatoriosController(IRelatoriosApi relatorios, TimeProvider relogio) : AuroraController
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(new RelatoriosViewModel(await relatorios.ListarAsync(ct), relogio.GetUtcNow().Year));

    /// <summary>Baixa o arquivo gerado. Um filtro recusado pela API (ex.: data inicial depois da final) volta à lista com o aviso.</summary>
    [HttpGet]
    public async Task<IActionResult> Baixar(
        string tipo, string formato, DateOnly? de, DateOnly? ate, string? status, int? ano, CancellationToken ct)
    {
        try
        {
            var filtro = new FiltroDeRelatorio(de, ate, string.IsNullOrWhiteSpace(status) ? null : status, ano);
            var arquivo = await relatorios.BaixarAsync(tipo, formato, filtro, ct);

            // Dados do negócio: nada de guardar em cache de navegador ou de proxy
            Response.Headers.CacheControl = "no-store";
            return File(arquivo.Conteudo, arquivo.TipoDeConteudo, arquivo.NomeDoArquivo);
        }
        catch (ApiException erro) when (erro.EhFalhaDeNegocio)
        {
            AvisarAtencao(erro.Detalhe);
            return RedirectToAction(nameof(Index));
        }
    }
}
