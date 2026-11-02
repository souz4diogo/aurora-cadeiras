namespace Aurora.Domain.Excecoes;

public enum TipoErro
{
    Validacao,
    NaoEncontrado,
    NaoAutenticado,
    Conflito,
    EntidadeNaoProcessavel,

    /// <summary>Autenticado, mas sem permissão para este recurso (403).</summary>
    Proibido,
}
