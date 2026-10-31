namespace Aurora.Domain.Entidades;

/// <summary>Linha da ficha técnica: quanto de uma matéria-prima o produto consome em uma estação.</summary>
public class FichaTecnicaItem
{
    private FichaTecnicaItem()
    {
    }

    public FichaTecnicaItem(int produtoId, int materiaPrimaId, byte estacaoId, decimal quantidade)
    {
        ProdutoId = produtoId;
        MateriaPrimaId = materiaPrimaId;
        EstacaoId = estacaoId;
        Quantidade = quantidade;
    }

    public int Id { get; private set; }

    public int ProdutoId { get; private set; }

    public int MateriaPrimaId { get; private set; }

    public byte EstacaoId { get; private set; }

    /// <summary>Quantidade consumida por unidade produzida, na unidade da matéria-prima.</summary>
    public decimal Quantidade { get; private set; }
}
