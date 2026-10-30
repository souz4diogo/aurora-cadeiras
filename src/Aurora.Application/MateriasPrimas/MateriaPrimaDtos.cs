using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;

namespace Aurora.Application.MateriasPrimas;

public sealed record MateriaPrimaDto(
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
    public static MateriaPrimaDto De(MateriaPrima m) => new(
        m.Id,
        m.Codigo,
        m.Nome,
        m.Unidade.ParaCodigo(),
        m.EstoqueAtual,
        m.EstoqueMinimo,
        m.CustoUnitario,
        m.FornecedorPreferencialId,
        m.Ativo,
        m.CriadoEm);
}

/// <summary>
/// Corpo de criação (POST) e de atualização (PUT) de matéria-prima. O saldo não entra aqui:
/// só muda por movimento de estoque. A unidade é o código: un, m, m2, m3 ou l.
/// </summary>
public sealed record MateriaPrimaRequest(
    string Codigo,
    string Nome,
    string Unidade,
    decimal EstoqueMinimo,
    decimal CustoUnitario,
    int? FornecedorPreferencialId);
