using Aurora.Application.Abstracoes;
using Aurora.Application.Autenticacao;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;
using FluentValidation;
using Moq;

namespace Aurora.Application.Tests;

public class AuthServiceTests
{
    private static readonly DateTime Agora = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IUsuarioRepository> _usuarios = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<ITokenService> _tokens = new();
    private readonly Mock<ISenhaHasher> _hasher = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUser> _usuarioAtual = new();
    private readonly Mock<IRelogio> _relogio = new();

    public AuthServiceTests()
    {
        _relogio.SetupGet(r => r.AgoraUtc).Returns(Agora);
        _tokens.Setup(t => t.GerarTokenDeAcesso(It.IsAny<Usuario>())).Returns(new TokenDeAcesso("jwt", 1800));
        _tokens.Setup(t => t.GerarRefreshToken()).Returns("refresh-novo");
        _tokens.Setup(t => t.CalcularHash(It.IsAny<string>())).Returns<string>(s => "hash:" + s);
    }

    private AuthService Servico() => new(
        _usuarios.Object, _refreshTokens.Object, _tokens.Object, _hasher.Object, _relogio.Object, _uow.Object,
        _usuarioAtual.Object, new LoginRequestValidator(), new RefreshRequestValidator(), new LogoutRequestValidator());

    private static Usuario NovoUsuario() => new("Maria", "maria@aurora.local", "hash-da-senha", PerfilUsuario.Operador, null);

    [Fact]
    public async Task Login_com_senha_correta_emite_tokens_e_guarda_apenas_o_hash_do_refresh()
    {
        var usuario = NovoUsuario();
        _usuarios.Setup(r => r.ObterPorEmailAsync("maria@aurora.local", default)).ReturnsAsync(usuario);
        _hasher.Setup(h => h.Verificar("hash-da-senha", "correta")).Returns(true);

        var resposta = await Servico().LoginAsync(new LoginRequest(" Maria@Aurora.local ", "correta"));

        Assert.Equal("jwt", resposta.AccessToken);
        Assert.Equal("refresh-novo", resposta.RefreshToken);
        _refreshTokens.Verify(
            r => r.Adicionar(It.Is<RefreshToken>(t => t.TokenHash == "hash:refresh-novo" && t.ExpiraEm == Agora.AddDays(7))),
            Times.Once);
        _uow.Verify(u => u.SalvarAsync(default), Times.Once);
    }

    [Fact]
    public async Task Login_com_senha_errada_conta_a_falha_e_devolve_credenciais_invalidas()
    {
        var usuario = NovoUsuario();
        _usuarios.Setup(r => r.ObterPorEmailAsync("maria@aurora.local", default)).ReturnsAsync(usuario);
        _hasher.Setup(h => h.Verificar(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().LoginAsync(new LoginRequest("maria@aurora.local", "errada")));

        Assert.Equal("CREDENCIAIS_INVALIDAS", erro.Codigo);
        Assert.Equal(TipoErro.NaoAutenticado, erro.Tipo);
        Assert.Equal(1, usuario.TentativasFalhas);
        _uow.Verify(u => u.SalvarAsync(default), Times.Once);
    }

    [Fact]
    public async Task Login_de_email_inexistente_devolve_o_mesmo_erro_sem_consultar_a_senha()
    {
        _usuarios.Setup(r => r.ObterPorEmailAsync(It.IsAny<string>(), default)).ReturnsAsync((Usuario?)null);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().LoginAsync(new LoginRequest("ninguem@aurora.local", "qualquer")));

        Assert.Equal("CREDENCIAIS_INVALIDAS", erro.Codigo);
        _hasher.Verify(h => h.Verificar(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Login_de_conta_bloqueada_nao_verifica_a_senha()
    {
        var usuario = NovoUsuario();
        for (var i = 0; i < 5; i++)
        {
            usuario.RegistrarFalhaDeLogin(Agora);
        }

        _usuarios.Setup(r => r.ObterPorEmailAsync("maria@aurora.local", default)).ReturnsAsync(usuario);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().LoginAsync(new LoginRequest("maria@aurora.local", "correta")));

        Assert.Equal("CONTA_BLOQUEADA", erro.Codigo);
        _hasher.Verify(h => h.Verificar(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Login_de_usuario_inativo_e_tratado_como_credencial_invalida()
    {
        var usuario = NovoUsuario();
        usuario.DefinirAtivo(false);
        _usuarios.Setup(r => r.ObterPorEmailAsync("maria@aurora.local", default)).ReturnsAsync(usuario);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().LoginAsync(new LoginRequest("maria@aurora.local", "correta")));

        Assert.Equal("CREDENCIAIS_INVALIDAS", erro.Codigo);
    }

    [Fact]
    public async Task Login_com_campos_vazios_falha_na_validacao()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Servico().LoginAsync(new LoginRequest("", "")));
    }

    [Fact]
    public async Task Refresh_com_token_revogado_revoga_todas_as_sessoes_do_usuario()
    {
        var revogado = new RefreshToken(7, "hash:antigo", Agora.AddDays(3));
        revogado.Revogar(Agora.AddMinutes(-5));
        _refreshTokens.Setup(r => r.ObterPorHashAsync("hash:antigo", default)).ReturnsAsync(revogado);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().RefreshAsync(new RefreshRequest("antigo")));

        Assert.Equal("REFRESH_INVALIDO", erro.Codigo);
        _refreshTokens.Verify(r => r.RevogarTodosDoUsuarioAsync(7, Agora, default), Times.Once);
        _uow.Verify(u => u.SalvarAsync(default), Times.Once);
    }

    [Fact]
    public async Task Refresh_valido_revoga_o_antigo_e_emite_um_novo_par()
    {
        var token = new RefreshToken(7, "hash:atual", Agora.AddDays(3));
        _refreshTokens.Setup(r => r.ObterPorHashAsync("hash:atual", default)).ReturnsAsync(token);
        _usuarios.Setup(r => r.ObterPorIdAsync(7, default)).ReturnsAsync(NovoUsuario());
        _refreshTokens.Setup(r => r.TentarRevogarAsync(token.Id, Agora, default)).ReturnsAsync(true);

        var resposta = await Servico().RefreshAsync(new RefreshRequest("atual"));

        Assert.Equal("refresh-novo", resposta.RefreshToken);
        _refreshTokens.Verify(r => r.TentarRevogarAsync(token.Id, Agora, default), Times.Once);
        _refreshTokens.Verify(r => r.Adicionar(It.IsAny<RefreshToken>()), Times.Once);
    }

    [Fact]
    public async Task Refresh_que_perde_a_corrida_pelo_mesmo_token_e_tratado_como_reuso()
    {
        var token = new RefreshToken(7, "hash:atual", Agora.AddDays(3));
        _refreshTokens.Setup(r => r.ObterPorHashAsync("hash:atual", default)).ReturnsAsync(token);
        _usuarios.Setup(r => r.ObterPorIdAsync(7, default)).ReturnsAsync(NovoUsuario());
        _refreshTokens.Setup(r => r.TentarRevogarAsync(token.Id, Agora, default)).ReturnsAsync(false);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().RefreshAsync(new RefreshRequest("atual")));

        Assert.Equal("REFRESH_INVALIDO", erro.Codigo);
        _refreshTokens.Verify(r => r.RevogarTodosDoUsuarioAsync(7, Agora, default), Times.Once);
        _refreshTokens.Verify(r => r.Adicionar(It.IsAny<RefreshToken>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_de_token_expirado_e_rejeitado()
    {
        var token = new RefreshToken(7, "hash:velho", Agora.AddMinutes(-1));
        _refreshTokens.Setup(r => r.ObterPorHashAsync("hash:velho", default)).ReturnsAsync(token);

        var erro = await Assert.ThrowsAsync<RegraDeNegocioException>(
            () => Servico().RefreshAsync(new RefreshRequest("velho")));

        Assert.Equal("REFRESH_INVALIDO", erro.Codigo);
    }

    [Fact]
    public async Task Logout_de_token_desconhecido_e_silencioso()
    {
        _refreshTokens.Setup(r => r.ObterPorHashAsync(It.IsAny<string>(), default)).ReturnsAsync((RefreshToken?)null);

        await Servico().LogoutAsync(new LogoutRequest("qualquer"));

        _uow.Verify(u => u.SalvarAsync(default), Times.Never);
    }
}
