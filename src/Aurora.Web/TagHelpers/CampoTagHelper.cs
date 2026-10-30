using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Aurora.Web.TagHelpers;

/// <summary>
/// Campo de formulário acessível: rótulo associado, campo e mensagem de erro ligados por <c>aria-describedby</c>
/// (junto com a dica opcional). Uso: <c>&lt;aurora-campo for="Nome" /&gt;</c>.
/// </summary>
[HtmlTargetElement("aurora-campo", TagStructure = TagStructure.WithoutEndTag)]
public sealed class CampoTagHelper(IHtmlGenerator gerador) : TagHelper
{
    [HtmlAttributeName("for")]
    public ModelExpression For { get; set; } = null!;

    /// <summary>Tipo do campo HTML (text, email, password...). Valores decimais usam "text" para respeitar a vírgula do pt-BR.</summary>
    [HtmlAttributeName("tipo")]
    public string Tipo { get; set; } = "text";

    [HtmlAttributeName("dica")]
    public string? Dica { get; set; }

    [HtmlAttributeName("tamanho-maximo")]
    public int? TamanhoMaximo { get; set; }

    /// <summary>Dica de teclado para dispositivos móveis (ex.: "decimal", "numeric").</summary>
    [HtmlAttributeName("modo")]
    public string? Modo { get; set; }

    [HtmlAttributeName("autocomplete")]
    public string? Autocomplete { get; set; }

    /// <summary>Formato do valor exibido (ex.: <c>{0:yyyy-MM-dd}</c>). Em campos de data (<c>tipo="date"</c>) o padrão é o formato ISO que o navegador exige.</summary>
    [HtmlAttributeName("formato")]
    public string? Formato { get; set; }

    [HtmlAttributeName("somente-leitura")]
    public bool SomenteLeitura { get; set; }

    [ViewContext]
    [HtmlAttributeNotBound]
    public ViewContext ViewContext { get; set; } = null!;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var idDoCampo = TagBuilder.CreateSanitizedId(For.Name, gerador.IdAttributeDotReplacement);
        var idDoErro = $"{idDoCampo}-erro";
        var idDaDica = $"{idDoCampo}-dica";

        var atributos = new Dictionary<string, object?>
        {
            ["class"] = "form-control",
            ["type"] = Tipo,
            ["aria-describedby"] = Dica is null ? idDoErro : $"{idDaDica} {idDoErro}",
        };
        if (TamanhoMaximo is int maximo)
        {
            atributos["maxlength"] = maximo;
        }

        if (Modo is not null)
        {
            atributos["inputmode"] = Modo;
        }

        if (Autocomplete is not null)
        {
            atributos["autocomplete"] = Autocomplete;
        }

        if (SomenteLeitura)
        {
            atributos["readonly"] = "readonly";
        }

        var rotulo = gerador.GenerateLabel(ViewContext, For.ModelExplorer, For.Name, labelText: null, new { @class = "form-label" });
        var formato = Formato ?? (Tipo == "date" ? "{0:yyyy-MM-dd}" : null);
        var campo = gerador.GenerateTextBox(ViewContext, For.ModelExplorer, For.Name, For.Model, formato, atributos);
        var erro = gerador.GenerateValidationMessage(
            ViewContext, For.ModelExplorer, For.Name, message: null, tag: "span", new { id = idDoErro, @class = "text-danger" });

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "mb-3");
        output.Content.AppendHtml(rotulo);
        output.Content.AppendHtml(campo);
        if (Dica is not null)
        {
            var dica = new TagBuilder("div");
            dica.AddCssClass("form-text");
            dica.Attributes["id"] = idDaDica;
            dica.InnerHtml.Append(Dica);
            output.Content.AppendHtml(dica);
        }

        output.Content.AppendHtml(erro);
    }
}
