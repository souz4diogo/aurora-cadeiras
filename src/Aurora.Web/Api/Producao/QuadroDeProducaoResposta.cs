namespace Aurora.Web.Api.Producao;

/// <param name="Colunas">Uma por estação que gera etapa, na ordem do layout da fábrica.</param>
/// <param name="Truncado">Há mais OPs em aberto do que o quadro mostra; aparecem as de previsão mais próxima.</param>
public sealed record QuadroDeProducaoResposta(
    IReadOnlyList<ColunaDoQuadroResposta> Colunas,
    int TotalDeOrdensAbertas,
    bool Truncado);
