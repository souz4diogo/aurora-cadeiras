using Aurora.Domain.Enums;

namespace Aurora.Application.Estoque;

public sealed record FiltroDeMovimentos(
    int? MateriaPrimaId,
    int? ProdutoId,
    TipoMovimento? Tipo,
    OrigemMovimento? Origem,
    DateOnly? De,
    DateOnly? Ate);
