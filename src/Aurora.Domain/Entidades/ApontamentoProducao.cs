using Aurora.Domain.Enums;

namespace Aurora.Domain.Entidades;

/// <summary>Registro imutável do início ou fim de uma etapa; o ClientRequestId torna o reenvio idempotente.</summary>
public class ApontamentoProducao
{
    private ApontamentoProducao()
    {
    }

    public long Id { get; private set; }

    public int OrdemProducaoEtapaId { get; private set; }

    public TipoApontamento Tipo { get; private set; }

    public int UsuarioId { get; private set; }

    /// <summary>Quando o operador fez a ação, segundo o relógio do aparelho (pode ter sido offline).</summary>
    public DateTime DataHoraCliente { get; private set; }

    public DateTime DataHoraServidor { get; private set; }

    public Guid ClientRequestId { get; private set; }
}
