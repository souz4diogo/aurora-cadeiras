namespace Aurora.Web.Api.Compras;

public sealed record PedidoCompraResumoResposta(
    int Id,
    string Numero,
    int FornecedorId,
    string FornecedorNome,
    string Status,
    DateOnly? DataPrevista,
    DateTime CriadoEm,
    decimal ValorTotal,
    int QuantidadeItens,
    bool Atrasado);

public sealed record PedidoCompraItemResposta(
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

public sealed record PedidoCompraResposta(
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
    IReadOnlyList<PedidoCompraItemResposta> Itens);
