using Aurora.Web.Api.Producao;
using Aurora.Web.Api.Qualidade;

namespace Aurora.Web.Models.Producao;

/// <param name="Inspecoes">Inspeções da OP; nulo quando o perfil não consulta qualidade (a seção some).</param>
public sealed record DetalhesDaOrdemViewModel(OrdemProducaoResposta Dados, IReadOnlyList<InspecaoResposta>? Inspecoes);
