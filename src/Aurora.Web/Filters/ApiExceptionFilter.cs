using Aurora.Web.Api.Comum;
using Aurora.Web.Logging;
using Aurora.Web.Seguranca;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace Aurora.Web.Filters;

/// <summary>
/// Converte as falhas da API que nenhuma tela tratou em páginas amigáveis: sessão expirada volta ao login, 403 e 404
/// viram páginas próprias e a API fora do ar mostra um aviso, sem expor detalhes técnicos. As falhas de validação e
/// de regra (400, 409, 422) são tratadas pelas próprias telas, que reexibem o formulário com a mensagem.
/// </summary>
public sealed class ApiExceptionFilter(IModelMetadataProvider metadados, ILogger<ApiExceptionFilter> log) : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case SessaoExpiradaException:
                await SessaoDeTokens.EncerrarAsync(context.HttpContext);
                var retorno = HttpMethods.IsGet(context.HttpContext.Request.Method)
                    ? context.HttpContext.Request.Path + context.HttpContext.Request.QueryString
                    : null;
                context.Result = new RedirectToActionResult("Login", "Conta", new { returnUrl = retorno, expirada = true });
                break;

            case ApiException { EhSemPermissao: true }:
                context.Result = Pagina(context, "AcessoNegado", StatusCodes.Status403Forbidden);
                break;

            case ApiException { EhNaoEncontrado: true }:
                context.Result = Pagina(context, "NaoEncontrado", StatusCodes.Status404NotFound);
                break;

            case ApiException erro:
                RegistrarFalha(erro);
                context.Result = Pagina(context, "ErroDaApi", StatusCodes.Status502BadGateway, erro);
                break;

            case HttpRequestException erro:
                RegistrarFalha(erro);
                context.Result = Pagina(context, "ApiIndisponivel", StatusCodes.Status503ServiceUnavailable);
                break;

            default:
                return;
        }

        context.ExceptionHandled = true;
    }

    private void RegistrarFalha(Exception erro) => LogMensagens.FalhaAoChamarApi(log, erro);

    private ViewResult Pagina(ExceptionContext context, string nomeDaView, int status, object? modelo = null) =>
        new()
        {
            ViewName = nomeDaView,
            StatusCode = status,
            ViewData = new ViewDataDictionary(metadados, context.ModelState) { Model = modelo },
        };
}
