using System.ComponentModel.DataAnnotations;

namespace Aurora.Infrastructure.Seguranca;

public sealed class JwtOptions
{
    public const string Secao = "Jwt";

    [Required]
    public string Issuer { get; set; } = "aurora-api";

    [Required]
    public string Audience { get; set; } = "aurora-clientes";

    /// <summary>Segredo HS256 (mínimo 32 caracteres). Vem de variável de ambiente/Key Vault, nunca do repositório.</summary>
    [Required]
    [MinLength(32)]
    public string Key { get; set; } = "";

    [Range(1, 1440)]
    public int ExpiracaoEmMinutos { get; set; } = 30;
}
