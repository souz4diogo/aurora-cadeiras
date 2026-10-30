using Aurora.Domain.Enums;

namespace Aurora.Domain.Entidades;

/// <summary>
/// Linha do livro-razão de estoque. É imutável: o banco recusa alterar ou apagar, e a aplicação só consulta;
/// quem grava são as procedures (correção é um novo movimento de ajuste).
/// </summary>
public class MovimentoEstoque
{
    private MovimentoEstoque()
    {
    }

    public long Id { get; private set; }

    /// <summary>Preenchido nos movimentos de matéria-prima (e nulo nos de produto acabado).</summary>
    public int? MateriaPrimaId { get; private set; }

    /// <summary>Preenchido nos movimentos de produto acabado (e nulo nos de matéria-prima).</summary>
    public int? ProdutoId { get; private set; }

    public TipoMovimento Tipo { get; private set; }

    /// <summary>Com sinal: positivo entra no estoque, negativo sai; no ajuste é a diferença aplicada.</summary>
    public decimal Quantidade { get; private set; }

    public OrigemMovimento Origem { get; private set; }

    /// <summary>Identificador do documento de origem (pedido de compra, OP etc.), quando houver.</summary>
    public int? OrigemId { get; private set; }

    public int? UsuarioId { get; private set; }

    public string? Motivo { get; private set; }

    public DateTime DataHora { get; private set; }
}
