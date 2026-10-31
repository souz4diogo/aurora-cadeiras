namespace Aurora.Web.Api.Clientes;

public sealed record ClienteResposta(
    int Id,
    string Nome,
    string CpfCnpj,
    string? Email,
    string? Telefone,
    string? Cidade,
    string? Uf,
    bool Ativo,
    DateTime CriadoEm);
