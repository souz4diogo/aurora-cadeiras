using Aurora.Web.Api.Comum;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Aurora.Web.Extensions;

public static class ModelStateExtensions
{
    /// <summary>
    /// Mostra no formulário o erro devolvido pela API: erros por campo vão para o campo de mesmo nome (a API devolve
    /// "Nome", "Itens[0].Quantidade" etc.) e a regra de negócio violada vai para o resumo geral.
    /// </summary>
    public static void AdicionarErrosDaApi(this ModelStateDictionary modelState, ApiException erro)
    {
        foreach (var (campo, mensagens) in erro.Erros)
        {
            foreach (var mensagem in mensagens)
            {
                modelState.AddModelError(campo, mensagem);
            }
        }

        if (erro.Erros.Count == 0)
        {
            modelState.AddModelError(string.Empty, erro.Detalhe);
        }
    }
}
