using Aurora.Domain.Enums;

namespace Aurora.Domain.Entidades;

/// <summary>Etapa de uma OP em uma estação. Só as procedures a alteram (RN03 a RN06).</summary>
public class OrdemProducaoEtapa
{
    private OrdemProducaoEtapa()
    {
    }

    public int Id { get; private set; }

    public int OrdemProducaoId { get; private set; }

    public byte EstacaoId { get; private set; }

    /// <summary>Posição da etapa dentro da OP, começando em 1: a etapa só inicia depois que a anterior terminar.</summary>
    public byte Sequencia { get; private set; }

    public StatusEtapa Status { get; private set; }

    public DateTime? IniciadaEm { get; private set; }

    public DateTime? ConcluidaEm { get; private set; }

    public int? OperadorId { get; private set; }
}
