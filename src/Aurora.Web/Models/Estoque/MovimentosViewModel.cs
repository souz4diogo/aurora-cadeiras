using Aurora.Web.Api.Comum;
using Aurora.Web.Api.Estoque;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Models.Estoque;

public sealed record MovimentosViewModel(
    Pagina<MovimentoEstoqueResposta> Pagina,
    FiltroDeMovimentos Filtro,
    IReadOnlyList<SelectListItem> MateriasPrimas);
