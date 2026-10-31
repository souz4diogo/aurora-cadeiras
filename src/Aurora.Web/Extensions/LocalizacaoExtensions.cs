using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace Aurora.Web.Extensions;

public static class LocalizacaoExtensions
{
    /// <summary>
    /// Toda a aplicação em pt-BR: números com vírgula decimal, datas dd/MM/aaaa e mensagens de validação do MVC em
    /// português. Sem isto, "10,5" digitado pelo usuário seria lido como "105" ou rejeitado.
    /// </summary>
    public static IApplicationBuilder UseAuroraLocalizacao(this IApplicationBuilder app) =>
        app.UseRequestLocalization(new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(Formatacao.PtBr),
            SupportedCultures = [Formatacao.PtBr],
            SupportedUICultures = [Formatacao.PtBr],
        });

    /// <summary>Troca as mensagens em inglês do model binding (ex.: "The value 'abc' is not valid") por mensagens em português.</summary>
    public static MvcOptions TraduzirMensagensDeValidacao(this MvcOptions opcoes)
    {
        var mensagens = opcoes.ModelBindingMessageProvider;
        mensagens.SetValueIsInvalidAccessor(valor => $"O valor '{valor}' não é válido.");
        mensagens.SetValueMustBeANumberAccessor(campo => $"O campo {campo} deve ser um número.");
        mensagens.SetAttemptedValueIsInvalidAccessor((valor, campo) => $"O valor '{valor}' não é válido para {campo}.");
        mensagens.SetMissingBindRequiredValueAccessor(campo => $"Informe {campo}.");
        mensagens.SetMissingKeyOrValueAccessor(() => "Informe um valor.");
        mensagens.SetMissingRequestBodyRequiredValueAccessor(() => "Corpo da requisição ausente.");
        mensagens.SetNonPropertyAttemptedValueIsInvalidAccessor(valor => $"O valor '{valor}' não é válido.");
        mensagens.SetNonPropertyUnknownValueIsInvalidAccessor(() => "O valor informado não é válido.");
        mensagens.SetNonPropertyValueMustBeANumberAccessor(() => "O valor deve ser um número.");
        mensagens.SetUnknownValueIsInvalidAccessor(campo => $"O valor informado não é válido para {campo}.");
        mensagens.SetValueMustNotBeNullAccessor(valor => $"O valor '{valor}' é obrigatório.");
        return opcoes;
    }
}
