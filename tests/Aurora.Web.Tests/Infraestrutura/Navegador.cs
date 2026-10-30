using System.Net;
using System.Text.RegularExpressions;

namespace Aurora.Web.Tests.Infraestrutura;

/// <summary>
/// Cliente HTTP que se comporta como um navegador nos testes: guarda cookies, não segue redirecionamentos sozinho e
/// sabe enviar formulários com o token anti-CSRF da página (obtido com um GET antes do POST).
/// </summary>
public sealed partial class Navegador(HttpClient cliente) : IDisposable
{
    [GeneratedRegex(@"<input[^>]*name=""__RequestVerificationToken""[^>]*value=""([^""]+)""")]
    private static partial Regex TokenAntiCsrf();

    public Task<HttpResponseMessage> GetAsync(string url) => cliente.GetAsync(url);

    /// <summary>GET que deve responder 200; devolve o HTML da página.</summary>
    public async Task<string> PaginaAsync(string url)
    {
        using var resposta = await cliente.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await resposta.Content.ReadAsStringAsync();
    }

    /// <summary>Abre o formulário em <paramref name="urlDoFormulario"/> (para pegar o token) e envia os campos para <paramref name="urlDeEnvio"/>.</summary>
    public async Task<HttpResponseMessage> EnviarAsync(string urlDoFormulario, string urlDeEnvio, IEnumerable<KeyValuePair<string, string>> campos)
    {
        var pagina = await PaginaAsync(urlDoFormulario);
        return await EnviarComTokenDaPaginaAsync(pagina, urlDeEnvio, campos);
    }

    /// <summary>Envia um formulário usando o token anti-CSRF presente no HTML já obtido.</summary>
    public Task<HttpResponseMessage> EnviarComTokenDaPaginaAsync(string html, string urlDeEnvio, IEnumerable<KeyValuePair<string, string>> campos)
    {
        var corpo = new List<KeyValuePair<string, string>>(campos) { new("__RequestVerificationToken", ExtrairToken(html)) };
        return cliente.PostAsync(urlDeEnvio, new FormUrlEncodedContent(corpo));
    }

    /// <summary>POST sem o token anti-CSRF, para provar que o servidor o exige.</summary>
    public Task<HttpResponseMessage> EnviarSemTokenAsync(string urlDeEnvio, IEnumerable<KeyValuePair<string, string>> campos) =>
        cliente.PostAsync(urlDeEnvio, new FormUrlEncodedContent(campos));

    public async Task<HttpResponseMessage> EntrarAsync(string email, string senha, string? returnUrl = null)
    {
        var url = returnUrl is null ? "/Conta/Login" : $"/Conta/Login?returnUrl={Uri.EscapeDataString(returnUrl)}";
        return await EnviarAsync(url, url, [new("Email", email), new("Senha", senha)]);
    }

    public static string ExtrairToken(string html)
    {
        var correspondencia = TokenAntiCsrf().Match(html);
        Assert.True(correspondencia.Success, "A página não tem o token anti-CSRF.");
        return WebUtility.HtmlDecode(correspondencia.Groups[1].Value);
    }

    public void Dispose() => cliente.Dispose();
}
