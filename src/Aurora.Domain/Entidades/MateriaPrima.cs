using Aurora.Domain.Enums;

namespace Aurora.Domain.Entidades;

public class MateriaPrima : EntidadeBase
{
    private MateriaPrima()
    {
    }

    public MateriaPrima(
        string codigo, string nome, UnidadeDeMedida unidade, decimal estoqueMinimo, decimal custoUnitario, int? fornecedorPreferencialId)
    {
        Codigo = codigo;
        Nome = nome;
        Unidade = unidade;
        EstoqueMinimo = estoqueMinimo;
        CustoUnitario = custoUnitario;
        FornecedorPreferencialId = fornecedorPreferencialId;
        Ativo = true;
    }

    public string Codigo { get; private set; } = "";

    public string Nome { get; private set; } = "";

    public UnidadeDeMedida Unidade { get; private set; }

    /// <summary>Saldo atual; só muda por movimento de estoque (livro-razão), nunca pelo cadastro.</summary>
    public decimal EstoqueAtual { get; private set; }

    public decimal EstoqueMinimo { get; private set; }

    public decimal CustoUnitario { get; private set; }

    public int? FornecedorPreferencialId { get; private set; }

    public bool Ativo { get; private set; }

    public void Atualizar(
        string codigo, string nome, UnidadeDeMedida unidade, decimal estoqueMinimo, decimal custoUnitario, int? fornecedorPreferencialId)
    {
        Codigo = codigo;
        Nome = nome;
        Unidade = unidade;
        EstoqueMinimo = estoqueMinimo;
        CustoUnitario = custoUnitario;
        FornecedorPreferencialId = fornecedorPreferencialId;
    }

    public void DefinirAtivo(bool ativo) => Ativo = ativo;
}
