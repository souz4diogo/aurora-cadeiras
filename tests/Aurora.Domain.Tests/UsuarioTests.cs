using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;

namespace Aurora.Domain.Tests;

public class UsuarioTests
{
    private static readonly DateTime Agora = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    private static Usuario Novo() => new("Maria", "maria@aurora.local", "hash", PerfilUsuario.Vendedor, null);

    [Fact]
    public void Quatro_falhas_nao_bloqueiam()
    {
        var usuario = Novo();

        for (var i = 0; i < 4; i++)
        {
            usuario.RegistrarFalhaDeLogin(Agora);
        }

        Assert.False(usuario.EstaBloqueado(Agora));
        Assert.Equal(4, usuario.TentativasFalhas);
    }

    [Fact]
    public void Quinta_falha_bloqueia_por_15_minutos()
    {
        var usuario = Novo();

        for (var i = 0; i < 5; i++)
        {
            usuario.RegistrarFalhaDeLogin(Agora);
        }

        Assert.True(usuario.EstaBloqueado(Agora.AddMinutes(14)));
        Assert.False(usuario.EstaBloqueado(Agora.AddMinutes(15)));
    }

    [Fact]
    public void Falha_depois_de_expirado_o_bloqueio_recomeca_a_contagem()
    {
        var usuario = Novo();
        for (var i = 0; i < 5; i++)
        {
            usuario.RegistrarFalhaDeLogin(Agora);
        }

        usuario.RegistrarFalhaDeLogin(Agora.AddMinutes(20));

        Assert.Equal(1, usuario.TentativasFalhas);
        Assert.False(usuario.EstaBloqueado(Agora.AddMinutes(20)));
    }

    [Fact]
    public void Login_com_sucesso_zera_falhas_e_registra_o_horario()
    {
        var usuario = Novo();
        usuario.RegistrarFalhaDeLogin(Agora);
        usuario.RegistrarFalhaDeLogin(Agora);

        usuario.RegistrarLoginComSucesso(Agora);

        Assert.Equal(0, usuario.TentativasFalhas);
        Assert.Equal(Agora, usuario.UltimoLoginEm);
    }

    [Fact]
    public void Trocar_senha_remove_o_bloqueio()
    {
        var usuario = Novo();
        for (var i = 0; i < 5; i++)
        {
            usuario.RegistrarFalhaDeLogin(Agora);
        }

        usuario.TrocarSenha("novo-hash");

        Assert.False(usuario.EstaBloqueado(Agora));
        Assert.Equal("novo-hash", usuario.SenhaHash);
    }

    [Fact]
    public void Refresh_token_revogado_ou_expirado_nao_esta_ativo()
    {
        var ativo = new RefreshToken(1, "h", Agora.AddDays(7));
        var expirado = new RefreshToken(1, "h", Agora.AddDays(-1));
        var revogado = new RefreshToken(1, "h", Agora.AddDays(7));
        revogado.Revogar(Agora);

        Assert.True(ativo.EstaAtivo(Agora));
        Assert.False(expirado.EstaAtivo(Agora));
        Assert.False(revogado.EstaAtivo(Agora));
    }

    [Fact]
    public void Revogar_duas_vezes_mantem_a_primeira_data()
    {
        var token = new RefreshToken(1, "h", Agora.AddDays(7));

        token.Revogar(Agora);
        token.Revogar(Agora.AddHours(1));

        Assert.Equal(Agora, token.RevogadoEm);
    }
}
