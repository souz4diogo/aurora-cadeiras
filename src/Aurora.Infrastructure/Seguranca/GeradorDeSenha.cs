using System.Security.Cryptography;
using Aurora.Application.Abstracoes;

namespace Aurora.Infrastructure.Seguranca;

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

        Embaralhar(caracteres);
        return new string(caracteres.ToArray());
    }

    /// <summary>Fisher-Yates, para a posição dos grupos de caracteres não ser previsível.</summary>
    private static void Embaralhar(List<char> caracteres)
    {
        for (var i = caracteres.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (caracteres[i], caracteres[j]) = (caracteres[j], caracteres[i]);
        }
    }

    private static char Sortear(string alfabeto) => alfabeto[RandomNumberGenerator.GetInt32(alfabeto.Length)];
}
