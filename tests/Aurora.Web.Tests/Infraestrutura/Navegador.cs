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

    /// <summary>HTML sem as entidades (o Razor escreve "á" como "&amp;#xE1;"), para os testes compararem com o texto real.</summary>
    public static string Decodificar(string html) => WebUtility.HtmlDecode(html);

    /// <summary>Destino do redirecionamento só com caminho e consulta (o middleware de cookie devolve a URL absoluta).</summary>
    public static string Destino(HttpResponseMessage resposta)
    {
        var destino = resposta.Headers.Location;
        return destino is null ? "" : destino.IsAbsoluteUri ? destino.PathAndQuery : destino.OriginalString;
    }

    /// <summary>Id do primeiro registro com link de edição (<c>/{controller}/Editar/{id}</c>) na página da lista.</summary>
    public static string IdDaPrimeiraEdicao(string html, string controller)
    {
        var correspondencia = Regex.Match(html, $@"/{controller}/Editar/(\d+)");
        Assert.True(correspondencia.Success, $"A lista não tem link de edição de {controller}.");
        return correspondencia.Groups[1].Value;
    }

    /// <summary>Id do primeiro produto com link de detalhes na lista.</summary>
    public static string IdDaPrimeiraDetalhe(string html)
    {
        var correspondencia = Regex.Match(html, @"/Produtos/Detalhes/(\d+)");
        Assert.True(correspondencia.Success, "A lista não tem link de detalhes de produto.");
        return correspondencia.Groups[1].Value;
    }

    public void Dispose() => cliente.Dispose();
}
