using Aurora.Web.Api.Comum;
using Aurora.Web.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Controllers;

/// <summary>Base dos controllers do painel: mensagens ao usuário e tratamento das falhas de negócio vindas da API.</summary>
public abstract class AuroraController : Controller
{
    public const string ChaveDeSucesso = "Sucesso";

    protected void AvisarSucesso(string mensagem) => TempData[ChaveDeSucesso] = mensagem;

    /// <summary>
    /// Executa a ação; se a API recusar por validação ou regra de negócio (400, 409, 422), mostra a mensagem no
    /// formulário e devolve o resultado de <paramref name="aoFalhar"/> (normalmente a mesma tela). Outras falhas
    /// seguem para o filtro global de erros.
    /// </summary>
    protected Task<IActionResult> TentarAsync(Func<Task<IActionResult>> acao, Func<IActionResult> aoFalhar) =>
        TentarAsync(acao, () => Task.FromResult(aoFalhar()));

    /// <summary>Como <see cref="TentarAsync(Func{Task{IActionResult}}, Func{IActionResult})"/>, para quando remontar a tela exige uma chamada assíncrona.</summary>
    protected async Task<IActionResult> TentarAsync(Func<Task<IActionResult>> acao, Func<Task<IActionResult>> aoFalhar)
    {
        try
        {
            return await acao();
        }
        catch (ApiException erro) when (erro.EhFalhaDeNegocio)
        {
            ModelState.AdicionarErrosDaApi(erro);
            return await aoFalhar();
        }
    }
}
