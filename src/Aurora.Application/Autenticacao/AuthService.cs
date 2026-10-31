using Aurora.Application.Abstracoes;
using Aurora.Application.Usuarios;
using Aurora.Domain.Entidades;
using Aurora.Domain.Excecoes;
using FluentValidation;

namespace Aurora.Application.Autenticacao;

public class AuthService(
    IUsuarioRepository usuarios,
    IRefreshTokenRepository refreshTokens,
    ITokenService tokens,
    ISenhaHasher hasher,
    IRelogio relogio,
    IUnitOfWork unitOfWork,
    ICurrentUser usuarioAtual,
    IValidator<LoginRequest> validadorLogin,
    IValidator<RefreshRequest> validadorRefresh,
    IValidator<LogoutRequest> validadorLogout)
{
    public static readonly TimeSpan DuracaoDoRefreshToken = TimeSpan.FromDays(7);

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        await validadorLogin.ValidateAndThrowAsync(request, ct);
        var agora = relogio.AgoraUtc;

        var usuario = await usuarios.ObterPorEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);
        if (usuario is null || !usuario.Ativo)
        {
            // Gasta o mesmo tempo de uma verificação real, para não revelar se o e-mail existe
            _ = hasher.Hash(request.Senha);
            throw CredenciaisInvalidas();
        }

        if (usuario.EstaBloqueado(agora))
        {
            throw new RegraDeNegocioException(
                "CONTA_BLOQUEADA",
                "Conta bloqueada temporariamente por excesso de tentativas. Tente novamente mais tarde.",
                TipoErro.NaoAutenticado);
        }

        if (!hasher.Verificar(usuario.SenhaHash, request.Senha))
        {
            usuario.RegistrarFalhaDeLogin(agora);
            await unitOfWork.SalvarAsync(ct);
            throw CredenciaisInvalidas();
        }

        usuario.RegistrarLoginComSucesso(agora);
        var resposta = EmitirParDeTokens(usuario, agora);
        await unitOfWork.SalvarAsync(ct);
        return resposta;
    }

    /// <summary>Refresh rotativo: cada uso revoga o token e emite um novo. Reuso de token revogado derruba todas as sessões.</summary>
    public async Task<TokenResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        await validadorRefresh.ValidateAndThrowAsync(request, ct);
        var agora = relogio.AgoraUtc;

        var token = await refreshTokens.ObterPorHashAsync(tokens.CalcularHash(request.RefreshToken), ct)
            ?? throw RefreshInvalido();

        if (token.RevogadoEm is not null)
        {
            await refreshTokens.RevogarTodosDoUsuarioAsync(token.UsuarioId, agora, ct);
            await unitOfWork.SalvarAsync(ct);
            throw RefreshInvalido();
        }

        if (!token.EstaAtivo(agora))
        {
            throw RefreshInvalido();
        }

        var usuario = await usuarios.ObterPorIdAsync(token.UsuarioId, ct);
        if (usuario is null || !usuario.Ativo || usuario.EstaBloqueado(agora))
        {
            throw RefreshInvalido();
        }

        // O par novo é gravado ANTES de reivindicar o token antigo. Se a gravação falhar, o usuário continua com a
        // sessão que tinha; e quem perde a corrida pelo mesmo token encontra o par do vencedor já gravado quando
        // revoga todas as sessões (a reação ao reuso não deixa o token novo de fora).
        var resposta = EmitirParDeTokens(usuario, agora);
        await unitOfWork.SalvarAsync(ct);

        // Atômico: se outra requisição usou este mesmo token primeiro, esta é tratada como reuso
        if (!await refreshTokens.TentarRevogarAsync(token.Id, agora, ct))
        {
            await refreshTokens.RevogarTodosDoUsuarioAsync(token.UsuarioId, agora, ct);
            await unitOfWork.SalvarAsync(ct);
            throw RefreshInvalido();
        }

        return resposta;
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken ct = default)
    {
        await validadorLogout.ValidateAndThrowAsync(request, ct);
        var token = await refreshTokens.ObterPorHashAsync(tokens.CalcularHash(request.RefreshToken), ct);
        if (token is null)
        {
            return;
        }

        token.Revogar(relogio.AgoraUtc);
        await unitOfWork.SalvarAsync(ct);
    }

    public async Task<MeDto> MeAsync(CancellationToken ct = default)
    {
        var id = usuarioAtual.UsuarioId ?? throw NaoAutenticado();
        var usuario = await usuarios.ObterPorIdAsync(id, ct) ?? throw NaoAutenticado();
        return new MeDto(UsuarioDto.De(usuario));
    }

    private TokenResponse EmitirParDeTokens(Usuario usuario, DateTime agora)
    {
        var acesso = tokens.GerarTokenDeAcesso(usuario);
        var refresh = tokens.GerarRefreshToken();
        refreshTokens.Adicionar(new RefreshToken(usuario.Id, tokens.CalcularHash(refresh), agora.Add(DuracaoDoRefreshToken)));
        return new TokenResponse(
            acesso.Valor,
            acesso.ExpiraEmSegundos,
            refresh,
            new UsuarioResumoDto(usuario.Id, usuario.Nome, usuario.Perfil.ToString()));
    }

    private static RegraDeNegocioException NaoAutenticado() =>
        new("NAO_AUTENTICADO", "Não autenticado.", TipoErro.NaoAutenticado);

    private static RegraDeNegocioException CredenciaisInvalidas() =>
        new("CREDENCIAIS_INVALIDAS", "E-mail ou senha inválidos.", TipoErro.NaoAutenticado);

    private static RegraDeNegocioException RefreshInvalido() =>
        new("REFRESH_INVALIDO", "Sessão inválida ou expirada. Faça login novamente.", TipoErro.NaoAutenticado);
}
