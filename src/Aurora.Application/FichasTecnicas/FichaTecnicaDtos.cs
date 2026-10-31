namespace Aurora.Application.FichasTecnicas;

public sealed record FichaTecnicaItemDto(
    int MateriaPrimaId,
    string MateriaPrimaCodigo,
    string MateriaPrimaNome,
    string Unidade,
    byte EstacaoId,
    string Estacao,
    decimal Quantidade);

public sealed record FichaTecnicaDto(int ProdutoId, IReadOnlyList<FichaTecnicaItemDto> Itens);

/// <summary>Material consumido por unidade do produto em uma estação do roteiro.</summary>
public sealed record FichaTecnicaItemRequest(int MateriaPrimaId, byte EstacaoId, decimal Quantidade);

/// <summary>Corpo do PUT: a lista informada <b>substitui</b> a ficha inteira (lista vazia limpa a ficha).</summary>
public sealed record FichaTecnicaRequest(IReadOnlyList<FichaTecnicaItemRequest> Itens);
