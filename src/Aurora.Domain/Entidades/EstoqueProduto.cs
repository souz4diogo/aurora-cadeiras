namespace Aurora.Domain.Entidades;

/// <summary>Saldo de produto acabado. Só as procedures e o trigger do livro-razão o alteram.</summary>
public class EstoqueProduto
{
    private EstoqueProduto()
    {
    }

    public int ProdutoId { get; private set; }

    public int Quantidade { get; private set; }

    /// <summary>Parte do saldo já prometida a pedidos confirmados (RN02).</summary>
    public int Reservado { get; private set; }

    public int Disponivel => Quantidade - Reservado;
}
