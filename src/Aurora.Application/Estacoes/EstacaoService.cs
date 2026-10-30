using Aurora.Application.Abstracoes;
using Aurora.Domain.Entidades;

namespace Aurora.Application.Estacoes;

public sealed record EstacaoDto(byte Id, string Nome, byte Ordem, bool GeraEtapa)
{
    public static EstacaoDto De(EstacaoProducao e) => new(e.Id, e.Nome, e.Ordem, e.GeraEtapa);
}

public class EstacaoService(IEstacaoRepository estacoes)
{
    /// <summary>Estações da linha de produção, na ordem do layout da fábrica (RF10).</summary>
    public async Task<IReadOnlyList<EstacaoDto>> ListarAsync(CancellationToken ct = default) =>
        (await estacoes.ListarAsync(ct)).OrderBy(e => e.Ordem).Select(EstacaoDto.De).ToList();
}
