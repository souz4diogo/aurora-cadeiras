using Aurora.Web.Api.Comum;

namespace Aurora.Web.Api.Estoque;

public interface IEstoqueApi
{
    Task<Pagina<SaldoDeMateriaPrimaResposta>> ListarSaldosDeMateriaPrimaAsync(
        string? busca, bool? critico, int pagina, CancellationToken ct = default);

    Task<Pagina<SaldoDeProdutoResposta>> ListarSaldosDeProdutoAsync(string? busca, int pagina, CancellationToken ct = default);

    Task<Pagina<MovimentoEstoqueResposta>> ListarMovimentosAsync(FiltroDeMovimentos filtro, int pagina, CancellationToken ct = default);

    Task<AjusteDeEstoqueResposta> AjustarAsync(AjusteDeEstoqueRequisicao dados, CancellationToken ct = default);

    /// <summary>Alertas ainda não lidos, o mais antigo primeiro.</summary>
    Task<Pagina<AlertaDeEstoqueResposta>> ListarAlertasAsync(int pagina, CancellationToken ct = default);

    Task<AlertaDeEstoqueResposta> MarcarAlertaComoLidoAsync(int id, CancellationToken ct = default);
}
