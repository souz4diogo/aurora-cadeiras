using FluentValidation;

namespace Aurora.Application.Comum;

public static class RegrasDeValidacao
{
    public const int TamanhoMaximoDoCodigo = 20;

    /// <summary>Código de cadastro (produto, matéria-prima): letras, números e hífen, ignorando espaços nas pontas.</summary>
    public static IRuleBuilderOptions<T, string> CodigoDeCadastro<T>(this IRuleBuilder<T, string> regra) =>
        regra
            .NotEmpty()
            .Must(codigo => codigo.Trim().Length <= TamanhoMaximoDoCodigo)
            .WithMessage($"O código aceita no máximo {TamanhoMaximoDoCodigo} caracteres.")
            .Matches("^\\s*[A-Za-z0-9-]+\\s*$")
            .WithMessage("O código aceita apenas letras, números e hífen.");
}
