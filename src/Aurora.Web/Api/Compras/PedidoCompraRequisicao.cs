namespace Aurora.Web.Api.Compras;

/// <param name="CustoUnitario">Nulo = a API usa o custo atual da matéria-prima.</param>
public sealed record PedidoCompraItemRequisicao(int MateriaPrimaId, decimal Quantidade, decimal? CustoUnitario);

public sealed record PedidoCompraRequisicao(int FornecedorId, DateOnly? DataPrevista, IReadOnlyList<PedidoCompraItemRequisicao> Itens);

public sealed record ReceberItemRequisicao(decimal Quantidade);

public sealed record FiltroDePedidosCompra(string? Status, int? FornecedorId, DateOnly? De, DateOnly? Ate, bool? Atrasados);
