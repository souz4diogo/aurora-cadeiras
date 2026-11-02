using Aurora.Web.Api.Auditoria;
using Aurora.Web.Api.Comum;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Aurora.Web.Models.Auditoria;

public sealed record AuditoriaViewModel(
    Pagina<AuditoriaResposta> Pagina,
    FiltroDeAuditoria Filtro,
    IReadOnlyList<string> Tabelas,
    IReadOnlyList<SelectListItem> Usuarios);
