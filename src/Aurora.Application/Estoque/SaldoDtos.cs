namespace Aurora.Application.Estoque;

public sealed record SaldoDeMateriaPrimaDto(
    int Id,
    string Codigo,
    string Nome,
    string Unidade,
    decimal EstoqueAtual,
    decimal EstoqueMinimo,
    bool Critico,
    int? FornecedorPreferencialId,
    string? FornecedorPreferencial);

public sealed record SaldoDeProdutoDto(
    int ProdutoId,
    string Codigo,
    string Nome,
    int Quantidade,
    int Reservado,
    int Disponivel);

public sealed record AlertaDeEstoqueDto(
    int Id,
    int MateriaPrimaId,
    string MateriaPrimaCodigo,
    string MateriaPrimaNome,
    string Unidade,
    decimal EstoqueAtual,
    decimal EstoqueMinimo,
    DateTime CriadoEm,
    DateTime? LidoEm,
    int? LidoPorId);
