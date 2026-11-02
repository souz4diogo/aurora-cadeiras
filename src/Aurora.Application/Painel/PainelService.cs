using Aurora.Application.Abstracoes;
using Aurora.Application.Comum;
using Aurora.Domain.Enums;

namespace Aurora.Application.Painel;

/// <summary>Painel gerencial (RF20): indicadores de agora, produção por estação, atrasos e consumo de material.</summary>
public class PainelService(
    IPainelRepository painel,
    IEstacaoRepository estacoes,
    ICurrentUser usuarioAtual,
    IRelogio relogio)
{
    public const int JanelaPadraoDeConsumoEmDias = 30;
    public const int JanelaMaximaDeConsumoEmDias = 365;
    public const int MateriaisNoGrafico = 10;

    /// <summary>O vendedor enxerga só os atrasos dos próprios pedidos; os demais perfis com acesso enxergam todos.</summary>
    private int? RestricaoDeVendedor =>
        usuarioAtual.Perfil == PerfilUsuario.Vendedor ? usuarioAtual.UsuarioId : null;

    public Task<ResumoDoPainelDto> ObterResumoAsync(CancellationToken ct = default) =>
        painel.ObterResumoAsync(relogio.AgoraUtc, ct);

    /// <summary>
    /// Todas as estações que geram etapa, na ordem do layout da fábrica, inclusive as vazias (com zeros), para o
    /// gráfico mostrar a linha inteira e não só onde há OP.
    /// </summary>
    public async Task<IReadOnlyList<ProducaoDaEstacaoDto>> ObterProducaoPorEstacaoAsync(CancellationToken ct = default)
    {
        var comOrdens = (await painel.ListarProducaoPorEstacaoAsync(ct)).ToDictionary(e => e.EstacaoId);
        return (await estacoes.ListarAsync(ct))
            .Where(e => e.GeraEtapa)
            .OrderBy(e => e.Ordem)
            .Select(e => comOrdens.GetValueOrDefault(e.Id) ?? new ProducaoDaEstacaoDto(e.Id, e.Nome, e.Ordem, 0, 0, 0, 0, null))
            .ToList();
    }

    public async Task<Pagina<PedidoAtrasadoDto>> ListarPedidosAtrasadosAsync(int pagina, int tamanho, CancellationToken ct = default)
    {
        (pagina, tamanho) = Paginacao.Normalizar(pagina, tamanho);
        var (itens, total) = await painel.ListarPedidosAtrasadosAsync(RestricaoDeVendedor, pagina, tamanho, ct);
        return new Pagina<PedidoAtrasadoDto>(itens, total, pagina, tamanho);
    }

    /// <summary>Os materiais que mais saíram para a produção nos últimos <paramref name="dias"/> dias (inclui hoje).</summary>
    public async Task<IReadOnlyList<ConsumoDeMaterialDto>> ObterConsumoAsync(int? dias, CancellationToken ct = default)
    {
        var janela = Math.Clamp(dias ?? JanelaPadraoDeConsumoEmDias, 1, JanelaMaximaDeConsumoEmDias);
        var ate = DateOnly.FromDateTime(relogio.AgoraUtc);
        return await painel.ListarConsumoAsync(ate.AddDays(1 - janela), ate, MateriaisNoGrafico, ct);
    }
}
