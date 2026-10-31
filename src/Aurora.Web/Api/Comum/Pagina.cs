namespace Aurora.Web.Api.Comum;

/// <summary>Página de resultados devolvida pela API: <c>{ items, total, page, pageSize }</c>.</summary>
public sealed record Pagina<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize)
{
    public int TotalDePaginas => Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(PageSize, 1)));

    public bool TemAnterior => Page > 1;

    public bool TemProxima => Page < TotalDePaginas;
}
