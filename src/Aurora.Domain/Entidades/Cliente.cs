namespace Aurora.Domain.Entidades;

public class Cliente : EntidadeBase
{
    private Cliente()
    {
    }

    public Cliente(string nome, string cpfCnpj, string? email, string? telefone, string? cidade, string? uf)
    {
        Nome = nome;
        CpfCnpj = cpfCnpj;
        Email = email;
        Telefone = telefone;
        Cidade = cidade;
        Uf = uf;
        Ativo = true;
    }

    public string Nome { get; private set; } = "";

    /// <summary>Somente dígitos: 11 (CPF) ou 14 (CNPJ).</summary>
    public string CpfCnpj { get; private set; } = "";

    public string? Email { get; private set; }

    public string? Telefone { get; private set; }

    public string? Cidade { get; private set; }

    public string? Uf { get; private set; }

    public bool Ativo { get; private set; }

    public void Atualizar(string nome, string cpfCnpj, string? email, string? telefone, string? cidade, string? uf)
    {
        Nome = nome;
        CpfCnpj = cpfCnpj;
        Email = email;
        Telefone = telefone;
        Cidade = cidade;
        Uf = uf;
    }

    public void DefinirAtivo(bool ativo) => Ativo = ativo;
}
