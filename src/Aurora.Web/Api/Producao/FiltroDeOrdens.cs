namespace Aurora.Web.Api.Producao;

/// <summary>Filtros da lista de OPs; campos nulos não filtram.</summary>
public sealed record FiltroDeOrdens(string? Status, byte? EstacaoId, int? PedidoId, bool? Atrasadas);
