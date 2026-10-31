namespace Aurora.Domain.Entidades;

/// <summary>
/// Aviso de matéria-prima no mínimo ou abaixo (RN08). O banco abre o alerta por trigger e mantém no máximo um
/// aberto por material; a aplicação só o marca como lido.
/// </summary>
public class AlertaEstoque
{
    private AlertaEstoque()
    {
    }

    public int Id { get; private set; }

    public int MateriaPrimaId { get; private set; }

    public DateTime CriadoEm { get; private set; }

    public DateTime? LidoEm { get; private set; }

    public int? LidoPorId { get; private set; }

    public bool Lido => LidoEm is not null;

    /// <summary>Marca como lido. Repetir é inofensivo: vale o primeiro registro de quem leu e de quando.</summary>
    public void MarcarComoLido(int usuarioId, DateTime agoraUtc)
    {
        if (Lido)
        {
            return;
        }

        LidoEm = agoraUtc;
        LidoPorId = usuarioId;
    }
}
