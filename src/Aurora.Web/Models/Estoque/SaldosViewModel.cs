using Aurora.Web.Api.Comum;
using Aurora.Web.Api.Estoque;

namespace Aurora.Web.Models.Estoque;

/// <param name="Critico">Filtro de saldo crítico: verdadeiro só os no mínimo ou abaixo, falso só os acima, nulo todos.</param>
public sealed record SaldosViewModel(Pagina<SaldoDeMateriaPrimaResposta> Pagina, string? Busca, bool? Critico);
