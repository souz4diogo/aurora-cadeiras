using Aurora.Domain.Enums;

namespace Aurora.Application.Pedidos;

/// <summary>Filtros da listagem; <see cref="VendedorId"/> restringe o resultado aos pedidos de um vendedor.</summary>
public sealed record FiltroDePedidos(
    int? VendedorId,
    StatusPedido? Status,
    int? ClienteId,
    DateOnly? De,
    DateOnly? Ate,
    bool? Atrasados,
    DateOnly Hoje);
