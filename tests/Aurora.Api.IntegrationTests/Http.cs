using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aurora.Application.Autenticacao;

namespace Aurora.Api.IntegrationTests;

internal static class Http
{
    public static async Task<JsonElement> ProblemaAsync(this HttpResponseMessage resposta)
    {
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        using var documento = await JsonDocument.ParseAsync(await resposta.Content.ReadAsStreamAsync());
        return documento.RootElement.Clone();
    }

    public static async Task<string?> CodigoAsync(this HttpResponseMessage resposta) =>
        (await resposta.ProblemaAsync()).GetProperty("code").GetString();

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient cliente, string email, string senha) =>
        cliente.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, senha));

    public static void AssertStatus(this HttpResponseMessage resposta, HttpStatusCode esperado) =>
        Assert.Equal(esperado, resposta.StatusCode);
}
