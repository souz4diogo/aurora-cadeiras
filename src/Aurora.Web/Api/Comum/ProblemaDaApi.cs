namespace Aurora.Web.Api.Comum;

/// <summary>Corpo de erro da API (RFC 7807 com "code" e "errors").</summary>
internal sealed record ProblemaDaApi(
    string? Title,
    string? Detail,
    string? Code,
    Dictionary<string, string[]>? Errors);
