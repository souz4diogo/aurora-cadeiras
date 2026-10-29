namespace Aurora.Application.Comum;

public sealed record Pagina<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);
