namespace Aurora.Web.Api.MateriasPrimas;

public sealed record MateriaPrimaResposta(
    int Id,
    string Codigo,
    string Nome,
    string Unidade,
    decimal EstoqueAtual,
    decimal EstoqueMinimo,
    decimal CustoUnitario,
    int? FornecedorPreferencialId,
    bool Ativo,
    DateTime CriadoEm)
{
    /// <summary>Saldo no mínimo ou abaixo dele (RN08): o painel destaca com texto, não só com cor.</summary>
    public bool EstaCritica => EstoqueAtual <= EstoqueMinimo;
}
