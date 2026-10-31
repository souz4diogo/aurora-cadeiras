namespace Aurora.Api.Extensions;

public static class ProblemDetailsExtensions
{
    /// <summary>ProblemDetails (RFC 7807) com os campos extras "traceId" e "code" também nas respostas de status sem corpo.</summary>
    public static IServiceCollection AddAuroraProblemDetails(this IServiceCollection services) =>
        services.AddProblemDetails(opcoes => opcoes.CustomizeProblemDetails = contexto =>
        {
            var problema = contexto.ProblemDetails;
            problema.Extensions["traceId"] = contexto.HttpContext.TraceIdentifier;
            problema.Extensions.TryAdd("code", CodigoPorStatus(problema.Status));
        });

    private static string CodigoPorStatus(int? status) => status switch
    {
        StatusCodes.Status401Unauthorized => "NAO_AUTENTICADO",
        StatusCodes.Status403Forbidden => "SEM_PERMISSAO",
        StatusCodes.Status404NotFound => "NAO_ENCONTRADO",
        StatusCodes.Status429TooManyRequests => "LIMITE_EXCEDIDO",
        _ => "ERRO",
    };
}
