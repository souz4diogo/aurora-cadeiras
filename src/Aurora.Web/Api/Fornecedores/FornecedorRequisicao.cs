namespace Aurora.Web.Api.Fornecedores;

public sealed record FornecedorRequisicao(string RazaoSocial, string Cnpj, string? Email, string? Telefone, bool ComunidadeTradicional);
