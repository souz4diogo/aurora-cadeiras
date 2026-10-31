using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Aurora.Application.Abstracoes;
using Aurora.Domain.Entidades;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Aurora.Infrastructure.Seguranca;

public sealed class JwtTokenService(IOptions<JwtOptions> opcoes, IRelogio relogio) : ITokenService
{
    private readonly JwtOptions _opcoes = opcoes.Value;

    public TokenDeAcesso GerarTokenDeAcesso(Usuario usuario)
    {
        var agora = relogio.AgoraUtc;
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opcoes.Key));
        var descritor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(CriarClaims(usuario)),
            Issuer = _opcoes.Issuer,
            Audience = _opcoes.Audience,
            IssuedAt = agora,
            NotBefore = agora,
            Expires = agora.AddMinutes(_opcoes.ExpiracaoEmMinutos),
            SigningCredentials = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256),
        };

        var valor = new JsonWebTokenHandler().CreateToken(descritor);
        return new TokenDeAcesso(valor, _opcoes.ExpiracaoEmMinutos * 60);
    }

    public string GerarRefreshToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public string CalcularHash(string refreshToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

    private static List<Claim> CriarClaims(Usuario usuario)
    {
        var claims = new List<Claim>
        {
            new(NomesDeClaims.UsuarioId, usuario.Id.ToString(CultureInfo.InvariantCulture)),
            new(NomesDeClaims.Nome, usuario.Nome),
            new(NomesDeClaims.Perfil, usuario.Perfil.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (usuario.ClienteId is int clienteId)
        {
            claims.Add(new Claim(NomesDeClaims.ClienteId, clienteId.ToString(CultureInfo.InvariantCulture)));
        }

        return claims;
    }
}
