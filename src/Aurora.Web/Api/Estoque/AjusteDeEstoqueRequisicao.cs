namespace Aurora.Web.Api.Estoque;

/// <summary>Saldo contado e motivo; a API grava a diferença como movimento de ajuste.</summary>
public sealed record AjusteDeEstoqueRequisicao(int MateriaPrimaId, decimal NovaQuantidade, string Motivo);
