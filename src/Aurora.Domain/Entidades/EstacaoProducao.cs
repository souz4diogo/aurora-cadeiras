namespace Aurora.Domain.Entidades;

/// <summary>Estação da linha de produção (dado de referência do seed: Corte, Montagem, Estofaria, Qualidade...).</summary>
public class EstacaoProducao
{
    private EstacaoProducao()
    {
    }

    public byte Id { get; private set; }

    public string Nome { get; private set; } = "";

    /// <summary>Posição da estação no layout da fábrica.</summary>
    public byte Ordem { get; private set; }

    /// <summary>Falso para estações que não geram etapa de produção (ex.: Expedição).</summary>
    public bool GeraEtapa { get; private set; }
}
