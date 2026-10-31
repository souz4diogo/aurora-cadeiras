using System.Net;
using Aurora.Web.Api.Autenticacao;
using Aurora.Web.Api.Comum;
using Aurora.Web.Logging;
using Aurora.Web.Models.Conta;
using Aurora.Web.Seguranca;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

public class ContaController(IAuthApi auth, TimeProvider relogio, ILogger<ContaController> log) : Controller
{
    private const string MensagemDeExcessoDeTentativas = "Muitas tentativas de entrada. Aguarde um minuto e tente de novo.";

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null, bool expirada = false)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirecionarParaDestino(returnUrl);
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { SessaoExpirada = expirada });
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel modelo, string? returnUrl = null, CancellationToken ct = default)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
        {
            return View(modelo);
        }

        try
        {
            var resposta = await auth.LoginAsync(modelo.Email.Trim(), modelo.Senha, ct);
            await SessaoDeTokens.GravarAsync(HttpContext, SessaoDeTokens.CriarUsuario(resposta.Usuario), resposta, relogio.GetUtcNow());
            return RedirecionarParaDestino(returnUrl);
        }
        catch (ApiException erro) when (erro.Status is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
        {
            // A API já devolve uma mensagem genérica (não revela se o e-mail existe) ou avisa da conta bloqueada
            ModelState.AddModelError(string.Empty, erro.Detalhe);
        }
        catch (ApiException erro) when (erro.Status == HttpStatusCode.TooManyRequests)
        {
            ModelState.AddModelError(string.Empty, MensagemDeExcessoDeTentativas);
        }

        modelo.Senha = "";
        return View(modelo);
    }

    [HttpPost]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var tokens = await SessaoDeTokens.LerAsync(HttpContext);
        if (tokens is not null)
        {
            try
            {
                await auth.SairAsync(tokens.AccessToken, tokens.RefreshToken, ct);
            }
            catch (Exception erro) when (erro is ApiException or HttpRequestException)
            {
                // Sair da web não pode depender da API: a sessão local é encerrada de qualquer forma
                LogMensagens.FalhaAoRevogarSessao(log, erro);
            }
        }

        await SessaoDeTokens.EncerrarAsync(HttpContext);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AcessoNegado() => View("~/Views/Shared/AcessoNegado.cshtml");

    private IActionResult RedirecionarParaDestino(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToAction("Index", "Home");
}
