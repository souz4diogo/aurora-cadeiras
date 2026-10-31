namespace Aurora.Application.Producao;

/// <summary>Coluna do quadro: uma estação com as OPs que estão nela agora.</summary>
public sealed record ColunaDoQuadroDto(
    byte EstacaoId,
    string EstacaoNome,
    byte Ordem,
    IReadOnlyList<OrdemProducaoResumoDto> Ordens);

/// <summary>
/// Quadro de produção (RF20). <paramref name="Truncado"/> avisa que há mais OPs em aberto do que o quadro mostra;
/// as exibidas são as de previsão mais próxima.
/// </summary>
public sealed record QuadroDeProducaoDto(
    IReadOnlyList<ColunaDoQuadroDto> Colunas,
    int TotalDeOrdensAbertas,
    bool Truncado);
