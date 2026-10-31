namespace Aurora.Web.Api.Autenticacao;

public sealed record TokenResposta(string AccessToken, int ExpiresInSeconds, string RefreshToken, UsuarioResumoResposta Usuario);
