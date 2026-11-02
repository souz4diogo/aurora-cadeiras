using Aurora.Web.Api.Comum;
using Aurora.Web.Api.Painel;

namespace Aurora.Web.Models.Painel;

/// <param name="Dias">Janela do consumo de materiais (7, 30 ou 90 dias).</param>
public sealed record PainelViewModel(
    ResumoDoPainelResposta Resumo,
    IReadOnlyList<ProducaoDaEstacaoResposta> Estacoes,
    IReadOnlyList<ConsumoDeMaterialResposta> Consumo,
    Pagina<PedidoAtrasadoResposta> Atrasados,
    int Dias)
{
    public static readonly int[] JanelasDeConsumo = [7, 30, 90];
    public const int JanelaPadrao = 30;
}
