namespace Aurora.Web.Api.Clientes;

public sealed record ClienteRequisicao(string Nome, string CpfCnpj, string? Email, string? Telefone, string? Cidade, string? Uf);
