using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;

namespace Aurora.Application.Compras;

/// <summary>Item do pedido. Sem <paramref name="CustoUnitario"/>, vale o custo atual da matéria-prima.</summary>
public sealed record PedidoCompraItemRequest(int MateriaPrimaId, decimal Quantidade, decimal? CustoUnitario);

public sealed record PedidoCompraRequest(int FornecedorId, DateOnly? DataPrevista, IReadOnlyList<PedidoCompraItemRequest> Itens);

/// <summary>Quantidade que chegou agora; pode ser parcial, até completar o que foi pedido.</summary>
public sealed record ReceberItemRequest(decimal Quantidade);

public sealed record FiltroDePedidosCompra(
    StatusPedidoCompra? Status, int? FornecedorId, DateOnly? De, DateOnly? Ate, bool? Atrasados, DateOnly Hoje);

/// <summary>Linha da listagem (leitura): já traz o nome do fornecedor e a contagem de itens.</summary>
public sealed record PedidoCompraResumo(
    int Id,
    string Numero,
    int FornecedorId,
    string FornecedorNome,
    StatusPedidoCompra Status,
    DateOnly? DataPrevista,
    DateTime CriadoEm,
    decimal ValorTotal,
    int QuantidadeItens)
{
    public bool EstaAtrasado(DateOnly hoje) =>
        Status is StatusPedidoCompra.Emitido or StatusPedidoCompra.ParcialmenteRecebido && DataPrevista < hoje;
}

public sealed record PedidoCompraResumoDto(
    int Id,
    string Numero,
    int FornecedorId,
    string FornecedorNome,
    string Status,
    DateOnly? DataPrevista,
    DateTime CriadoEm,
    decimal ValorTotal,
    int QuantidadeItens,
    bool Atrasado)
{
    public static PedidoCompraResumoDto De(PedidoCompraResumo r, DateOnly hoje) => new(
        r.Id,
        r.Numero,
        r.FornecedorId,
        r.FornecedorNome,
        r.Status.ToString(),
        r.DataPrevista,
        r.CriadoEm,
        r.ValorTotal,
        r.QuantidadeItens,
        r.EstaAtrasado(hoje));
}

public sealed record PedidoCompraItemDto(
    int Id,
    int MateriaPrimaId,
    string MateriaPrimaCodigo,
    string MateriaPrimaNome,
    string Unidade,
    decimal Quantidade,
    decimal CustoUnitario,
    decimal QuantidadeRecebida,
    decimal Saldo,
    decimal Subtotal);

public sealed record PedidoCompraDto(
    int Id,
    string Numero,
    int FornecedorId,
    string FornecedorNome,
    bool ComunidadeTradicional,
    int CompradorId,
    string? CompradorNome,
    string Status,
    DateOnly? DataPrevista,
    DateTime CriadoEm,
    decimal ValorTotal,
    bool Atrasado,
    bool PodeEmitir,
    bool PodeCancelar,
    bool PodeReceber,
    IReadOnlyList<PedidoCompraItemDto> Itens);
