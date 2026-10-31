using Aurora.Web.Api.Comum;
using Aurora.Web.Api.Estacoes;
using Aurora.Web.Api.Producao;

namespace Aurora.Web.Models.Producao;

public sealed record OrdensListaViewModel(
    Pagina<OrdemProducaoResumoResposta> Pagina,
    FiltroDeOrdens Filtro,
    IReadOnlyList<EstacaoResposta> Estacoes);
