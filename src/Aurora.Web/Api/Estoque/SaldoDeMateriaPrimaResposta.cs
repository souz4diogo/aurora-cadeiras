namespace Aurora.Web.Api.Estoque;

public sealed record SaldoDeMateriaPrimaResposta(
    int Id,
    string Codigo,
    string Nome,
    string Unidade,
    decimal EstoqueAtual,
    decimal EstoqueMinimo,
    bool Critico,
    int? FornecedorPreferencialId,
    string? FornecedorPreferencial);
