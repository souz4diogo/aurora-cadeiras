using Aurora.Web.Api.Clientes;
using Aurora.Web.Api.Comum;

namespace Aurora.Web.Models.Clientes;

public sealed record ClientesListaViewModel(Pagina<ClienteResposta> Pagina, string? Busca, bool? Ativo);
