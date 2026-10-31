using Aurora.Application.Abstracoes;
using Aurora.Application.Comum;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;

namespace Aurora.Application.Producao;

public class OrdemProducaoService(
    IProducaoRepository producao,
    IEstacaoRepository estacoes,
    ICurrentUser usuarioAtual,
    IRelogio relogio)
{
    /// <summary>Máximo de OPs desenhadas no quadro; acima disso ele avisa que está truncado.</summary>
    public const int LimiteDoQuadro = 300;

    /// <summary>O vendedor enxerga só as OPs dos próprios pedidos; os demais perfis com acesso enxergam todas.</summary>
    private int? RestricaoDeVendedor =>
        usuarioAtual.Perfil == PerfilUsuario.Vendedor ? usuarioAtual.UsuarioId : null;

    public async Task<Pagina<OrdemProducaoResumoDto>> ListarAsync(
        StatusOrdemProducao? status,
        byte? estacaoId,
        int? pedidoId,
        bool? atrasadas,
        int pagina,
        int tamanho,
        CancellationToken ct = default)
    {
        (pagina, tamanho) = Paginacao.Normalizar(pagina, tamanho);
        var agora = relogio.AgoraUtc;
        var filtro = new FiltroDeOrdens(RestricaoDeVendedor, status, estacaoId, pedidoId, atrasadas, DateOnly.FromDateTime(agora));
        var (itens, total) = await producao.ListarOrdensAsync(filtro, pagina, tamanho, ct);
        return new Pagina<OrdemProducaoResumoDto>(
            itens.Select(o => OrdemProducaoResumoDto.De(o, agora)).ToList(), total, pagina, tamanho);
    }

    /// <summary>
    /// OPs em aberto agrupadas pela estação em que estão agora, na ordem do layout da fábrica. OP sem etapa
    /// pendente ou em andamento (já toda concluída) não aparece.
    /// </summary>
    public async Task<QuadroDeProducaoDto> ObterQuadroAsync(CancellationToken ct = default)
    {
        var agora = relogio.AgoraUtc;
        var filtro = new FiltroDeOrdens(
            RestricaoDeVendedor, null, null, null, null, DateOnly.FromDateTime(agora), SomenteAbertas: true);
        var (ordens, total) = await producao.ListarOrdensAsync(filtro, 1, LimiteDoQuadro, ct);

        var porEstacao = ordens
            .Where(o => o.EtapaAtual is not null)
            .ToLookup(o => o.EtapaAtual!.EstacaoId);
        var colunas = (await estacoes.ListarAsync(ct))
            .Where(e => e.GeraEtapa)
            .OrderBy(e => e.Ordem)
            .Select(e => new ColunaDoQuadroDto(
                e.Id, e.Nome, e.Ordem, porEstacao[e.Id].Select(o => OrdemProducaoResumoDto.De(o, agora)).ToList()))
            .ToList();

        return new QuadroDeProducaoDto(colunas, total, Truncado: total > ordens.Count);
    }

    public async Task<OrdemProducaoDto> ObterAsync(int id, CancellationToken ct = default)
    {
        var detalhe = await producao.ObterOrdemAsync(id, RestricaoDeVendedor, ct)
            ?? throw new RegraDeNegocioException(
                "ORDEM_PRODUCAO_NAO_ENCONTRADA", "Ordem de produção não encontrada.", TipoErro.NaoEncontrado);

        return new OrdemProducaoDto(
            OrdemProducaoResumoDto.De(detalhe.Resumo, relogio.AgoraUtc),
            detalhe.CodigoQr,
            detalhe.ClienteNome,
            detalhe.Etapas.Select(EtapaDto.De).ToList(),
            detalhe.Apontamentos,
            detalhe.Perdas);
    }
}
