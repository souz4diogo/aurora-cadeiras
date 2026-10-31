namespace Aurora.Web.Api.Pedidos;

/// <summary>Filtros da lista de pedidos; campos nulos não filtram.</summary>
public sealed record FiltroDePedidos(string? Status, int? ClienteId, DateOnly? De, DateOnly? Ate, bool? Atrasados);
