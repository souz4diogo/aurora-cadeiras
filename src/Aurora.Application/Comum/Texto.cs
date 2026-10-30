namespace Aurora.Application.Comum;

public static class Texto
{
    /// <summary>Texto sem espaços nas pontas, ou nulo se vier vazio (campo opcional não preenchido).</summary>
    public static string? OuNulo(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>Mantém só os dígitos (aceita CPF, CNPJ e telefone digitados com máscara).</summary>
    public static string SomenteDigitos(string? valor) =>
        valor is null ? "" : new string(valor.Where(char.IsAsciiDigit).ToArray());
}
