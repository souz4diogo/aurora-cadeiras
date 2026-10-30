using Aurora.Web.Api.Comum;
using Aurora.Web.Api.MateriasPrimas;

namespace Aurora.Web.Models.MateriasPrimas;

public sealed record MateriasPrimasListaViewModel(Pagina<MateriaPrimaResposta> Pagina, string? Busca, bool? Ativo);
