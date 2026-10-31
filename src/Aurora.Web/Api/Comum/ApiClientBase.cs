using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Aurora.Web.Api.Comum;

/// <summary>Base dos clientes tipados da API: serialização, montagem de consulta e conversão de erros em <see cref="ApiException"/>.</summary>
public abstract class ApiClientBase(HttpClient http)
{
    private const int TamanhoDaPaginaDeEscolha = 100;
    private const int MaximoDePaginas = 20;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected Task<T> HttpGetAsync<T>(string url, CancellationToken ct) => EnviarAsync<T>(HttpMethod.Get, url, null, ct);

    protected Task<T> HttpPostAsync<T>(string url, object corpo, CancellationToken ct) => EnviarAsync<T>(HttpMethod.Post, url, corpo, ct);

    protected Task<T> HttpPutAsync<T>(string url, object corpo, CancellationToken ct) => EnviarAsync<T>(HttpMethod.Put, url, corpo, ct);

    protected Task<T> HttpPatchAsync<T>(string url, object corpo, CancellationToken ct) =>
        EnviarAsync<T>(HttpMethod.Patch, url, corpo, ct);

    /// <summary>
    /// Lê todas as páginas de uma lista da API (100 por vez, até <see cref="MaximoDePaginas"/>) para preencher
    /// listas de escolha em formulários.
    /// </summary>
    protected async Task<IReadOnlyList<T>> TodasAsPaginasAsync<T>(
        string rota, (string Nome, object? Valor)[] filtros, CancellationToken ct)
    {
        var itens = new List<T>();
        for (var pagina = 1; pagina <= MaximoDePaginas; pagina++)
        {
            var parametros = filtros.Concat([("page", (object?)pagina), ("pageSize", TamanhoDaPaginaDeEscolha)]).ToArray();
            var resposta = await HttpGetAsync<Pagina<T>>(rota + Consulta(parametros), ct);
            itens.AddRange(resposta.Items);
            if (!resposta.TemProxima)
            {
                break;
            }
        }

        return itens;
    }

    /// <summary>Monta "?a=1&amp;b=x" ignorando os parâmetros nulos ou vazios e usando sempre a cultura invariante.</summary>
    protected static string Consulta(params (string Nome, object? Valor)[] parametros)
    {
        var construtor = new StringBuilder();
        foreach (var (nome, valor) in parametros)
        {
            var texto = valor switch
            {
                null => null,
                bool b => b ? "true" : "false",
                DateOnly data => data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                IFormattable formatavel => formatavel.ToString(null, CultureInfo.InvariantCulture),
                _ => valor.ToString(),
            };
            if (string.IsNullOrWhiteSpace(texto))
            {
                continue;
            }

            construtor.Append(construtor.Length == 0 ? '?' : '&').Append(Uri.EscapeDataString(nome)).Append('=').Append(Uri.EscapeDataString(texto));
        }

        return construtor.ToString();
    }

    /// <summary>Envia uma requisição cuja resposta de sucesso não tem corpo (204), com o Bearer informado à mão.</summary>
    protected async Task EnviarSemRespostaAsync(HttpMethod metodo, string url, object? corpo, string? bearer, CancellationToken ct)
    {
        using var requisicao = new HttpRequestMessage(metodo, url);
        if (corpo is not null)
        {
            requisicao.Content = JsonContent.Create(corpo, options: Json);
        }

        if (bearer is not null)
        {
            requisicao.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearer);
        }

        using var resposta = await http.SendAsync(requisicao, ct);
        if (!resposta.IsSuccessStatusCode)
        {
            throw await CriarExcecaoAsync(resposta, ct);
        }
    }

    private async Task<T> EnviarAsync<T>(HttpMethod metodo, string url, object? corpo, CancellationToken ct)
    {
        using var requisicao = new HttpRequestMessage(metodo, url);
        if (corpo is not null)
        {
            requisicao.Content = JsonContent.Create(corpo, options: Json);
        }

        using var resposta = await http.SendAsync(requisicao, ct);
        if (!resposta.IsSuccessStatusCode)
        {
            throw await CriarExcecaoAsync(resposta, ct);
        }

        return (await resposta.Content.ReadFromJsonAsync<T>(Json, ct))!;
    }

    private static async Task<ApiException> CriarExcecaoAsync(HttpResponseMessage resposta, CancellationToken ct)
    {
        ProblemaDaApi? problema = null;
        try
        {
            problema = await resposta.Content.ReadFromJsonAsync<ProblemaDaApi>(Json, ct);
        }
        catch (JsonException)
        {
            // Corpo que não é ProblemDetails (ex.: erro do proxy): segue só com o status
        }

        return new ApiException(
            resposta.StatusCode,
            problema?.Code ?? "ERRO",
            problema?.Title ?? "Não foi possível concluir a operação",
            problema?.Detail ?? "A API não respondeu como esperado. Tente novamente.",
            problema?.Errors ?? []);
    }
}
