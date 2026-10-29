namespace Aurora.Domain.Entidades;

public class RefreshToken
{
    private RefreshToken()
    {
    }

    public RefreshToken(int usuarioId, string tokenHash, DateTime expiraEmUtc)
    {
        UsuarioId = usuarioId;
        TokenHash = tokenHash;
        ExpiraEm = expiraEmUtc;
    }

    public long Id { get; private set; }

    public int UsuarioId { get; private set; }

    public string TokenHash { get; private set; } = "";

    public DateTime ExpiraEm { get; private set; }

    public DateTime? RevogadoEm { get; private set; }

    public DateTime CriadoEm { get; private set; }

    public bool EstaAtivo(DateTime agoraUtc) => RevogadoEm is null && ExpiraEm > agoraUtc;

    public void Revogar(DateTime agoraUtc) => RevogadoEm ??= agoraUtc;
}
