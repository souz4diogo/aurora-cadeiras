namespace Aurora.Web.Api.Estoque;

/// <summary>Filtros do extrato; campos nulos não filtram.</summary>
public sealed record FiltroDeMovimentos(int? MateriaPrimaId, string? Tipo, string? Origem, DateOnly? De, DateOnly? Ate);
