using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;

namespace Aurora.Domain.Entidades;

/// <summary>Refugo ou retrabalho informado numa etapa (RF12).</summary>
public class RegistroPerda
{
    public const int TamanhoMaximoDoMotivo = 200;

    private RegistroPerda()
    {
    }

    public RegistroPerda(int ordemProducaoEtapaId, TipoPerda tipo, int quantidade, string motivo, int usuarioId)
    {
        if (quantidade <= 0)
        {
            throw new RegraDeNegocioException(
                "QUANTIDADE_INVALIDA", "A quantidade da perda deve ser maior que zero.", TipoErro.EntidadeNaoProcessavel);
        }

        OrdemProducaoEtapaId = ordemProducaoEtapaId;
        Tipo = tipo;
        Quantidade = quantidade;
        Motivo = motivo;
        UsuarioId = usuarioId;
    }

    public int Id { get; private set; }

    public int OrdemProducaoEtapaId { get; private set; }

    public TipoPerda Tipo { get; private set; }

    public int Quantidade { get; private set; }

    public string Motivo { get; private set; } = null!;

    public int UsuarioId { get; private set; }

    /// <summary>Preenchida pelo banco (UTC).</summary>
    public DateTime CriadoEm { get; private set; }
}
