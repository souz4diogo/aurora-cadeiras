using Aurora.Domain.Enums;

namespace Aurora.Domain.Entidades;

/// <summary>
/// Ordem de produção. É gerada e avançada pelas procedures (confirmação do pedido, início e fim de etapa,
/// inspeção); a aplicação só consulta.
/// </summary>
public class OrdemProducao
{
    private readonly List<OrdemProducaoEtapa> _etapas = [];

    private OrdemProducao()
    {
    }

    public int Id { get; private set; }

    /// <summary>Gerado pelo banco a partir de uma sequência (OP-2026-00001).</summary>
    public string Numero { get; private set; } = null!;

    public int PedidoItemId { get; private set; }

    public int ProdutoId { get; private set; }

    public int Quantidade { get; private set; }

    public StatusOrdemProducao Status { get; private set; }

    /// <summary>Identificador impresso na etiqueta; não revela o número da OP nem permite adivinhar outro.</summary>
    public Guid CodigoQr { get; private set; }

    public DateOnly? PrevisaoConclusao { get; private set; }

    public DateTime CriadaEm { get; private set; }

    public DateTime? ConcluidaEm { get; private set; }

    public IReadOnlyCollection<OrdemProducaoEtapa> Etapas => _etapas;
}
