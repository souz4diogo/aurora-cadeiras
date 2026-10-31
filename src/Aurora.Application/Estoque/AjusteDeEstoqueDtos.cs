using Aurora.Application.MateriasPrimas;

namespace Aurora.Application.Estoque;

/// <summary>Ajuste de saldo: informa o saldo que deveria existir (contagem física) e o motivo; a diferença vira um movimento.</summary>
public sealed record AjusteDeEstoqueRequest(int MateriaPrimaId, decimal NovaQuantidade, string Motivo);

public sealed record AjusteDeEstoqueDto(
    MateriaPrimaDto MateriaPrima,
    decimal SaldoAnterior,
    decimal Diferenca);
