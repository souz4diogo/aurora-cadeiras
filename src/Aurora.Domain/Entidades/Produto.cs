using Aurora.Domain.Enums;

namespace Aurora.Domain.Entidades;

public class Produto : EntidadeBase
{
    private Produto()
    {
    }

    public Produto(string codigo, string nome, LinhaProduto linha, decimal precoVenda)
    {
        Codigo = codigo;
        Nome = nome;
        Linha = linha;
        PrecoVenda = precoVenda;
        Ativo = true;
    }

    public string Codigo { get; private set; } = "";

    public string Nome { get; private set; } = "";

    public LinhaProduto Linha { get; private set; }

    public decimal PrecoVenda { get; private set; }

    public bool Ativo { get; private set; }

    public void Atualizar(string codigo, string nome, LinhaProduto linha, decimal precoVenda)
    {
        Codigo = codigo;
        Nome = nome;
        Linha = linha;
        PrecoVenda = precoVenda;
    }

    public void DefinirAtivo(bool ativo) => Ativo = ativo;
}
