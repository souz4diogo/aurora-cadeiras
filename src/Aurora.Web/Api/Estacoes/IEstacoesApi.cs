namespace Aurora.Web.Api.Estacoes;

public interface IEstacoesApi
{
    /// <summary>Estações da linha de produção, na ordem do layout da fábrica.</summary>
    Task<IReadOnlyList<EstacaoResposta>> ListarAsync(CancellationToken ct = default);
}
