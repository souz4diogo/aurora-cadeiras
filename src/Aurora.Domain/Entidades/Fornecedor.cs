namespace Aurora.Domain.Entidades;

public class Fornecedor : EntidadeBase
{
    private Fornecedor()
    {
    }

    public Fornecedor(string razaoSocial, string cnpj, string? email, string? telefone, bool comunidadeTradicional)
    {
        RazaoSocial = razaoSocial;
        Cnpj = cnpj;
        Email = email;
        Telefone = telefone;
        ComunidadeTradicional = comunidadeTradicional;
        Ativo = true;
    }

    public string RazaoSocial { get; private set; } = "";

    /// <summary>Somente os 14 dígitos.</summary>
    public string Cnpj { get; private set; } = "";

    public string? Email { get; private set; }

    public string? Telefone { get; private set; }

    /// <summary>Marca fornecedores de comunidades tradicionais (relatório de responsabilidade social).</summary>
    public bool ComunidadeTradicional { get; private set; }

    public bool Ativo { get; private set; }

    public void Atualizar(string razaoSocial, string cnpj, string? email, string? telefone, bool comunidadeTradicional)
    {
        RazaoSocial = razaoSocial;
        Cnpj = cnpj;
        Email = email;
        Telefone = telefone;
        ComunidadeTradicional = comunidadeTradicional;
    }

    public void DefinirAtivo(bool ativo) => Ativo = ativo;
}
