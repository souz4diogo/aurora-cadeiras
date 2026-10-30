using Aurora.Domain.Enums;

namespace Aurora.Application.Estoque;

/// <summary>Movimento do extrato (leitura), já com o código e o nome do item e o nome de quem fez.</summary>
public sealed record MovimentoEstoqueResumo(
    long Id,
    DateTime DataHora,
    TipoMovimento Tipo,
    OrigemMovimento Origem,
    int? OrigemId,
    int? MateriaPrimaId,
    int? ProdutoId,
    string ItemCodigo,
    string ItemNome,
    decimal Quantidade,
    int? UsuarioId,
    string? UsuarioNome,
    string? Motivo);

public sealed record MovimentoEstoqueDto(
    long Id,
    DateTime DataHora,
    string Tipo,
    string Origem,
    int? OrigemId,
    string ItemTipo,
    int ItemId,
    string ItemCodigo,
    string ItemNome,
    decimal Quantidade,
    int? UsuarioId,
    string? UsuarioNome,
    string? Motivo)
{
    public const string TipoMateriaPrima = "MateriaPrima";
    public const string TipoProduto = "Produto";

    public static MovimentoEstoqueDto De(MovimentoEstoqueResumo m) => new(
        m.Id,
        m.DataHora,
        m.Tipo.ToString(),
        m.Origem.ToString(),
        m.OrigemId,
        m.MateriaPrimaId is null ? TipoProduto : TipoMateriaPrima,
        m.MateriaPrimaId ?? m.ProdutoId ?? 0,
        m.ItemCodigo,
        m.ItemNome,
        m.Quantidade,
        m.UsuarioId,
        m.UsuarioNome,
        m.Motivo);
}
