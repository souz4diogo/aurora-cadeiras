namespace Aurora.Domain.Entidades;

/// <summary>
/// Linha do histórico de alterações (RNF10). É gravada somente pelos triggers do banco, a partir do usuário que a API
/// informa no <c>SESSION_CONTEXT</c>; a aplicação apenas consulta (o papel da aplicação tem INSERT, UPDATE e DELETE
/// negados nesta tabela).
/// </summary>
public class RegistroDeAuditoria
{
    private RegistroDeAuditoria()
    {
    }

    public long Id { get; private set; }

    public string Tabela { get; private set; } = "";

    /// <summary>Chave do registro alterado, como texto.</summary>
    public string Registro { get; private set; } = "";

    /// <summary>I (inclusão), U (alteração) ou D (exclusão).</summary>
    public string Acao { get; private set; } = "";

    /// <summary>Quem fez a alteração; nulo quando veio de fora da API (script, manutenção).</summary>
    public int? UsuarioId { get; private set; }

    public DateTime DataHora { get; private set; }

    /// <summary>JSON da linha antes da alteração (nulo na inclusão).</summary>
    public string? Antes { get; private set; }

    /// <summary>JSON da linha depois da alteração (nulo na exclusão).</summary>
    public string? Depois { get; private set; }
}
