namespace Aurora.Application.Autenticacao;

/// <summary>Regra de senha do sistema (docs/06 §2): no mínimo 10 caracteres. Um lugar só para todas as entradas de senha.</summary>
public static class PoliticaDeSenha
{
    public const int TamanhoMinimo = 10;

    /// <summary>Limite alto o bastante para frases longas e baixo o bastante para não servir de ataque de negação de serviço no hash.</summary>
    public const int TamanhoMaximo = 200;

    public static bool Atende(string? senha) =>
        senha is not null && senha.Length >= TamanhoMinimo && senha.Length <= TamanhoMaximo && !string.IsNullOrWhiteSpace(senha);

    public static string Descricao => $"A senha deve ter de {TamanhoMinimo} a {TamanhoMaximo} caracteres.";
}
