using Aurora.Application.Usuarios;

namespace Aurora.Application.Autenticacao;

public sealed record LoginRequest(string Email, string Senha);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record UsuarioResumoDto(int Id, string Nome, string Perfil);

public sealed record TokenResponse(string AccessToken, int ExpiresInSeconds, string RefreshToken, UsuarioResumoDto Usuario);

public sealed record MeDto(UsuarioDto Usuario);
