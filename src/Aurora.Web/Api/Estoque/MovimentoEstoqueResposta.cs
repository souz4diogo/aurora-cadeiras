namespace Aurora.Web.Api.Estoque;

public sealed record MovimentoEstoqueResposta(
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
    string? Motivo);
