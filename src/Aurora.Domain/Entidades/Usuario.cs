using Aurora.Domain.Enums;

namespace Aurora.Domain.Entidades;

public class Usuario : EntidadeBase
{
    public const int MaximoDeFalhas = 5;
    public static readonly TimeSpan DuracaoDoBloqueio = TimeSpan.FromMinutes(15);

    private Usuario()
    {
    }

    public Usuario(string nome, string email, string senhaHash, PerfilUsuario perfil, int? clienteId)
    {
        Nome = nome;
        Email = email;
        SenhaHash = senhaHash;
        Perfil = perfil;
        ClienteId = clienteId;
        Ativo = true;
    }

    public string Nome { get; private set; } = "";

    public string Email { get; private set; } = "";

    public string SenhaHash { get; private set; } = "";

    public PerfilUsuario Perfil { get; private set; }

    public int? ClienteId { get; private set; }

    public bool Ativo { get; private set; }

    public byte TentativasFalhas { get; private set; }

    public DateTime? BloqueadoAte { get; private set; }

    public DateTime? UltimoLoginEm { get; private set; }

    public bool EstaBloqueado(DateTime agoraUtc) => BloqueadoAte is not null && BloqueadoAte > agoraUtc;

    /// <summary>Conta a falha e bloqueia por 15 minutos ao atingir 5 tentativas.</summary>
    public void RegistrarFalhaDeLogin(DateTime agoraUtc)
    {
        if (BloqueadoAte is not null && BloqueadoAte <= agoraUtc)
        {
            TentativasFalhas = 0;
            BloqueadoAte = null;
        }

        TentativasFalhas++;
        if (TentativasFalhas >= MaximoDeFalhas)
        {
            BloqueadoAte = agoraUtc.Add(DuracaoDoBloqueio);
        }
    }

    public void RegistrarLoginComSucesso(DateTime agoraUtc)
    {
        TentativasFalhas = 0;
        BloqueadoAte = null;
        UltimoLoginEm = agoraUtc;
    }

    public void Atualizar(string nome, string email, PerfilUsuario perfil, int? clienteId)
    {
        Nome = nome;
        Email = email;
        Perfil = perfil;
        ClienteId = clienteId;
    }

    public void DefinirAtivo(bool ativo) => Ativo = ativo;

    public void TrocarSenha(string novoHash)
    {
        SenhaHash = novoHash;
        TentativasFalhas = 0;
        BloqueadoAte = null;
    }
}
