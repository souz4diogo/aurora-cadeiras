using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Aurora.Application.Abstracoes;
using Aurora.Domain.Entidades;
using Microsoft.AspNetCore.Identity;
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
        var expira = agora.AddMinutes(_opcoes.ExpiracaoEmMinutos);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Name, usuario.Nome),
            new("role", usuario.Perfil.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        if (usuario.ClienteId is int clienteId)
        {
            claims.Add(new Claim("clienteId", clienteId.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opcoes.Key));
        var descritor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _opcoes.Issuer,
            Audience = _opcoes.Audience,
            IssuedAt = agora,
            NotBefore = agora,
            Expires = expira,
            SigningCredentials = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256),
        };

        var valor = new JsonWebTokenHandler().CreateToken(descritor);
        return new TokenDeAcesso(valor, _opcoes.ExpiracaoEmMinutos * 60);
    }

    public string GerarRefreshToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public string CalcularHash(string refreshToken) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}

/// <summary>PBKDF2 (HMAC-SHA512, salt aleatório) via PasswordHasher do ASP.NET Core Identity.</summary>
public sealed class SenhaHasher : ISenhaHasher
{
    private readonly PasswordHasher<object> _hasher = new();
    private static readonly object Contexto = new();

    public string Hash(string senha) => _hasher.HashPassword(Contexto, senha);

    public bool Verificar(string hash, string senha) =>
        _hasher.VerifyHashedPassword(Contexto, hash, senha) != PasswordVerificationResult.Failed;
}

public sealed class GeradorDeSenha : IGeradorDeSenha
{
    private const string Minusculas = "abcdefghijkmnopqrstuvwxyz";
    private const string Maiusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digitos = "23456789";
    private const string Simbolos = "!@#$%*?";
    private const int Tamanho = 16;

    public string GerarProvisoria()
    {
        var todos = Minusculas + Maiusculas + Digitos + Simbolos;
        var caracteres = new List<char>(Tamanho)
        {
            Sortear(Minusculas), Sortear(Maiusculas), Sortear(Digitos), Sortear(Simbolos),
        };
        while (caracteres.Count < Tamanho)
        {
            caracteres.Add(Sortear(todos));
        }

        // Embaralha para a posição dos grupos não ser previsível
        for (var i = caracteres.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (caracteres[i], caracteres[j]) = (caracteres[j], caracteres[i]);
        }

        return new string(caracteres.ToArray());
    }

    private static char Sortear(string alfabeto) => alfabeto[RandomNumberGenerator.GetInt32(alfabeto.Length)];
}

public sealed class RelogioDoSistema : IRelogio
{
    public DateTime AgoraUtc => DateTime.UtcNow;
}
