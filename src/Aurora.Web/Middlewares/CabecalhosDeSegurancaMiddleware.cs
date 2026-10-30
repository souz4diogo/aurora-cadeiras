namespace Aurora.Web.Middlewares;

/// <summary>
/// Cabeçalhos de segurança do painel. A política de conteúdo só permite recursos do próprio site (sem scripts nem
/// estilos embutidos), por isso as telas não podem usar &lt;script&gt; nem style="" no HTML.
/// </summary>
public sealed class CabecalhosDeSegurancaMiddleware(RequestDelegate proximo)
{
    private const string PoliticaDeConteudo =
        "default-src 'self'; img-src 'self' data:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    public Task InvokeAsync(HttpContext contexto)
    {
        contexto.Response.OnStarting(() =>
        {
            var cabecalhos = contexto.Response.Headers;
            cabecalhos["Content-Security-Policy"] = PoliticaDeConteudo;
            cabecalhos["X-Content-Type-Options"] = "nosniff";
            cabecalhos["X-Frame-Options"] = "DENY";
            cabecalhos["Referrer-Policy"] = "no-referrer";
            return Task.CompletedTask;
        });
        return proximo(contexto);
    }
}
