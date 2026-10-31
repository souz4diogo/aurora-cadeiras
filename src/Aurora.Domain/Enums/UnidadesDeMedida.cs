using System.Diagnostics.CodeAnalysis;

namespace Aurora.Domain.Enums;

/// <summary>Converte a unidade de medida de/para o código gravado no banco (un, m, m2, m3, l).</summary>
public static class UnidadesDeMedida
{
    private static readonly Dictionary<UnidadeDeMedida, string> Codigos = new()
    {
        [UnidadeDeMedida.Unidade] = "un",
        [UnidadeDeMedida.Metro] = "m",
        [UnidadeDeMedida.MetroQuadrado] = "m2",
        [UnidadeDeMedida.MetroCubico] = "m3",
        [UnidadeDeMedida.Litro] = "l",
    };

    public static string ParaCodigo(this UnidadeDeMedida unidade) => Codigos[unidade];

    public static bool TentarObter(string? codigo, [NotNullWhen(true)] out UnidadeDeMedida? unidade)
    {
        foreach (var (chave, valor) in Codigos)
        {
            if (string.Equals(valor, codigo, StringComparison.Ordinal))
            {
                unidade = chave;
                return true;
            }
        }

        unidade = null;
        return false;
    }

    public static UnidadeDeMedida DeCodigo(string codigo) =>
        TentarObter(codigo, out var unidade)
            ? unidade.Value
            : throw new ArgumentOutOfRangeException(nameof(codigo), codigo, "Unidade de medida desconhecida.");
}
