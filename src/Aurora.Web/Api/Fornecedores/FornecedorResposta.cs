namespace Aurora.Web.Api.Fornecedores;

public sealed record FornecedorResposta(
    int Id,
    string RazaoSocial,
    string Cnpj,
    string? Email,
    string? Telefone,
    bool ComunidadeTradicional,
    bool Ativo,
    DateTime CriadoEm);
