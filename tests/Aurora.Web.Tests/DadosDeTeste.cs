using System.Globalization;

namespace Aurora.Web.Tests;

internal static class DadosDeTeste
{
    /// <summary>Sequência aleatória de dígitos (CPF/CNPJ fictícios), única o bastante para cada teste.</summary>
    public static string Digitos(int quantidade) =>
        string.Concat(Guid.NewGuid().ToByteArray().Take(quantidade).Select(b => (b % 10).ToString(CultureInfo.InvariantCulture)));
}
