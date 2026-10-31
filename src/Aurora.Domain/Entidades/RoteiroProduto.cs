namespace Aurora.Domain.Entidades;

/// <summary>Uma passagem do produto por uma estação; a sequência define a ordem das etapas da OP.</summary>
public class RoteiroProduto
{
    private RoteiroProduto()
    {
    }

    public RoteiroProduto(int produtoId, byte estacaoId, byte sequencia)
    {
        ProdutoId = produtoId;
        EstacaoId = estacaoId;
        Sequencia = sequencia;
    }

    public int ProdutoId { get; private set; }

    public byte EstacaoId { get; private set; }

    /// <summary>Posição da estação no roteiro, começando em 1.</summary>
    public byte Sequencia { get; private set; }
}
