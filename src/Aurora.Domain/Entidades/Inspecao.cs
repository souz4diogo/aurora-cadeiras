using Aurora.Domain.Enums;

namespace Aurora.Domain.Entidades;

/// <summary>
/// Inspeção de qualidade de uma OP (RF18). É gravada somente por sp_RegistrarInspecao, que também conclui a OP
/// ou reabre as etapas (RN06); a aplicação apenas consulta o histórico.
/// </summary>
public class Inspecao
{
    public const int TamanhoMaximoDaObservacao = 300;

    private Inspecao()
    {
    }

    public int Id { get; private set; }

    public int OrdemProducaoId { get; private set; }

    public int InspetorId { get; private set; }

    public ResultadoInspecao Resultado { get; private set; }

    public byte? EstacaoRetrabalhoId { get; private set; }

    public string? Observacao { get; private set; }

    /// <summary>Preenchida pelo banco (UTC).</summary>
    public DateTime DataHora { get; private set; }
}
