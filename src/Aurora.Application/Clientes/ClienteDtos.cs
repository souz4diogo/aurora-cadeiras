using Aurora.Domain.Entidades;

namespace Aurora.Application.Clientes;

public sealed record ClienteDto(
    int Id,
    string Nome,
    string CpfCnpj,
    string? Email,
    string? Telefone,
    string? Cidade,
    string? Uf,
    bool Ativo,
    DateTime CriadoEm)
{
    public static ClienteDto De(Cliente c) =>
        new(c.Id, c.Nome, c.CpfCnpj, c.Email, c.Telefone, c.Cidade, c.Uf, c.Ativo, c.CriadoEm);
}

/// <summary>Corpo de criação (POST) e de atualização (PUT) de cliente; o CPF/CNPJ pode vir com máscara.</summary>
public sealed record ClienteRequest(string Nome, string CpfCnpj, string? Email, string? Telefone, string? Cidade, string? Uf);
