namespace Aurora.Application.Comum;

/// <summary>Converte o nome de um enum vindo de fora (JSON, query) no valor, num único lugar.</summary>
public static class EnumPorNome
{
    /// <summary>
    /// Aceita só o nome (sem diferenciar maiúsculas, ignorando espaços nas pontas). O <c>Enum.TryParse</c> sozinho
    /// aceitaria também números ("1") e valores fora do enum, o que não queremos na entrada da API.
    /// </summary>
    public static bool TentarObter<T>(string? texto, out T valor)
        where T : struct, Enum
    {
        var nome = Enum.GetNames<T>().FirstOrDefault(n => string.Equals(n, texto?.Trim(), StringComparison.OrdinalIgnoreCase));
        valor = nome is null ? default : Enum.Parse<T>(nome);
        return nome is not null;
    }
}
