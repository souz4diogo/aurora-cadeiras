using Aurora.Application.Abstracoes;
using Microsoft.AspNetCore.Identity;

namespace Aurora.Infrastructure.Seguranca;

/// <summary>PBKDF2 (HMAC-SHA512, salt aleatório) via PasswordHasher do ASP.NET Core Identity.</summary>
public sealed class SenhaHasher : ISenhaHasher
{
    private static readonly object Contexto = new();

    private readonly PasswordHasher<object> _hasher = new();

    public string Hash(string senha) => _hasher.HashPassword(Contexto, senha);

    public bool Verificar(string hash, string senha) =>
        _hasher.VerifyHashedPassword(Contexto, hash, senha) != PasswordVerificationResult.Failed;
}
