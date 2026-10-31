namespace Aurora.Domain.Excecoes;

/// <summary>Violação de regra de negócio com código estável (ex.: RN04) para a API traduzir em ProblemDetails.</summary>
public class RegraDeNegocioException(string codigo, string mensagem, TipoErro tipo = TipoErro.Conflito)
    : Exception(mensagem)
{
    public string Codigo { get; } = codigo;

    public TipoErro Tipo { get; } = tipo;
}
