namespace Aurora.Web.Models.Navegacao;

/// <summary>Dados do controle de paginação das listas; os links mantêm os filtros que já estão na consulta.</summary>
public sealed record PaginacaoViewModel(int Pagina, int TotalDePaginas, int Total);
