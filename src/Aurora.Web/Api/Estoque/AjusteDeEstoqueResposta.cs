using Aurora.Web.Api.MateriasPrimas;

namespace Aurora.Web.Api.Estoque;

public sealed record AjusteDeEstoqueResposta(MateriaPrimaResposta MateriaPrima, decimal SaldoAnterior, decimal Diferenca);
