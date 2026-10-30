namespace Aurora.Web.Api.Produtos;

public sealed record FichaTecnicaItemResposta(
    int MateriaPrimaId,
    string MateriaPrimaCodigo,
    string MateriaPrimaNome,
    string Unidade,
    byte EstacaoId,
    string Estacao,
    decimal Quantidade);
