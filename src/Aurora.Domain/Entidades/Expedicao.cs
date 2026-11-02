namespace Aurora.Domain.Entidades;

/// <summary>
/// Saída de um pedido Pronto (RF19, RN07). É gravada somente por sp_Expedir, que baixa o estoque de produto acabado
/// e muda o pedido para Expedido; a aplicação apenas consulta.
/// </summary>
public class Expedicao
{
    public const int TamanhoMaximoDaTransportadora = 80;
    public const int TamanhoMaximoDoCodigoDeRastreio = 40;

    private Expedicao()
    {
    }

    public int Id { get; private set; }

    public int PedidoId { get; private set; }

    public int UsuarioId { get; private set; }

    public string? Transportadora { get; private set; }

    public string? CodigoRastreio { get; private set; }

    /// <summary>Preenchida pelo banco (UTC).</summary>
    public DateTime DataHora { get; private set; }
}
