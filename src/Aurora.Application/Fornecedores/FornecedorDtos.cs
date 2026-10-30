using Aurora.Domain.Entidades;

namespace Aurora.Application.Fornecedores;

public sealed record FornecedorDto(
    int Id,
    string RazaoSocial,
    string Cnpj,
    string? Email,
    string? Telefone,
    bool ComunidadeTradicional,
    bool Ativo,
    DateTime CriadoEm)
{
    public static FornecedorDto De(Fornecedor f) =>
        new(f.Id, f.RazaoSocial, f.Cnpj, f.Email, f.Telefone, f.ComunidadeTradicional, f.Ativo, f.CriadoEm);
}

/// <summary>Corpo de criação (POST) e de atualização (PUT) de fornecedor; o CNPJ pode vir com máscara.</summary>
public sealed record FornecedorRequest(string RazaoSocial, string Cnpj, string? Email, string? Telefone, bool ComunidadeTradicional);
