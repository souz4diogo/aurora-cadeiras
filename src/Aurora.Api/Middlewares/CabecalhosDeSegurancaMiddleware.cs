namespace Aurora.Api.Middlewares;

/// <summary>Cabeçalhos de segurança básicos para respostas da API.</summary>
public sealed class CabecalhosDeSegurancaMiddleware(RequestDelegate proximo)
{
    public Task InvokeAsync(HttpContext contexto)
    {
        contexto.Response.OnStarting(() =>
        {
            var cabecalhos = contexto.Response.Headers;
            cabecalhos["X-Content-Type-Options"] = "nosniff";
            cabecalhos["X-Frame-Options"] = "DENY";
            cabecalhos["Referrer-Policy"] = "no-referrer";
            cabecalhos["Cache-Control"] = "no-store";
            return Task.CompletedTask;
        });
        return proximo(contexto);
    }
}
