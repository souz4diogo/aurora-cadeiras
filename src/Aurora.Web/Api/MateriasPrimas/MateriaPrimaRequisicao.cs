namespace Aurora.Web.Api.MateriasPrimas;

public sealed record MateriaPrimaRequisicao(
    string Codigo,
    string Nome,
    string Unidade,
    decimal EstoqueMinimo,
    decimal CustoUnitario,
    int? FornecedorPreferencialId);
