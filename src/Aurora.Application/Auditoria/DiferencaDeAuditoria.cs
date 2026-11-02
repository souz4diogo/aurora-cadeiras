using System.Text.Json;

namespace Aurora.Application.Auditoria;

/// <summary>
/// Compara o JSON "antes" e "depois" gravado pelos triggers e devolve os campos que mudaram, para a tela mostrar o
/// que aconteceu sem o usuário ler JSON. Função pura: o mesmo par de textos sempre dá a mesma lista.
/// </summary>
public static class DiferencaDeAuditoria
{
    public static IReadOnlyList<CampoAlteradoDto> Calcular(string? antes, string? depois)
    {
        var anteriores = Ler(antes);
        var posteriores = Ler(depois);

        var campos = anteriores.Keys.Concat(posteriores.Keys.Where(k => !anteriores.ContainsKey(k))).ToList();
        var resultado = new List<CampoAlteradoDto>();
        foreach (var campo in campos)
        {
            var temAntes = anteriores.TryGetValue(campo, out var valorAntes);
            var temDepois = posteriores.TryGetValue(campo, out var valorDepois);

            // Alteração com os dois lados: só entra o que mudou. Inclusão e exclusão listam tudo.
            if (temAntes && temDepois && valorAntes == valorDepois)
            {
                continue;
            }

            resultado.Add(new CampoAlteradoDto(campo, valorAntes, valorDepois));
        }

        return resultado;
    }

    public static string NomeDaAcao(string acao) => acao switch
    {
        "I" => "Inclusão",
        "U" => "Alteração",
        "D" => "Exclusão",
        _ => acao,
    };

    /// <summary>Campos do objeto JSON como texto; JSON ausente ou inválido vira lista vazia (a linha continua aparecendo).</summary>
    private static Dictionary<string, string?> Ler(string? json)
    {
        var campos = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return campos;
        }

        try
        {
            using var documento = JsonDocument.Parse(json);
            if (documento.RootElement.ValueKind != JsonValueKind.Object)
            {
                return campos;
            }

            foreach (var propriedade in documento.RootElement.EnumerateObject())
            {
                campos[propriedade.Name] = propriedade.Value.ValueKind switch
                {
                    JsonValueKind.Null => null,
                    JsonValueKind.String => propriedade.Value.GetString(),
                    _ => propriedade.Value.GetRawText(),
                };
            }
        }
        catch (JsonException)
        {
            // Trigger gravou algo que não é JSON: mostra a linha sem detalhes em vez de derrubar a consulta
        }

        return campos;
    }
}
