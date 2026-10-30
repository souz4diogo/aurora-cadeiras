using System.ComponentModel.DataAnnotations;
using FluentValidation;

namespace Aurora.Application.Comum;

public static class RegrasDeValidacao
{
    public const int TamanhoMaximoDoCodigo = 20;

    /// <summary>Código de cadastro (produto, matéria-prima): letras, números e hífen, ignorando espaços nas pontas.</summary>
    public static IRuleBuilderOptions<T, string> CodigoDeCadastro<T>(this IRuleBuilder<T, string> regra) =>
        regra
            .NotEmpty()
            .Must(codigo => (codigo?.Trim().Length ?? 0) <= TamanhoMaximoDoCodigo)
            .WithMessage($"O código aceita no máximo {TamanhoMaximoDoCodigo} caracteres.")
            .Matches("^\\s*[A-Za-z0-9-]+\\s*$")
            .WithMessage("O código aceita apenas letras, números e hífen.");

    /// <summary>
    /// CPF (11 dígitos) ou CNPJ (14), com ou sem máscara. Só o formato é conferido: os dígitos verificadores não,
    /// porque o seed usa CNPJs fictícios (DECISOES A21).
    /// </summary>
    public static IRuleBuilderOptions<T, string> DocumentoComTamanho<T>(this IRuleBuilder<T, string> regra, params int[] tamanhos) =>
        regra
            .NotEmpty()
            .Must(documento => tamanhos.Contains(Texto.SomenteDigitos(documento).Length))
            .WithMessage($"Informe um documento com {string.Join(" ou ", tamanhos)} dígitos.");

    /// <summary>E-mail opcional: só é validado quando preenchido.</summary>
    public static IRuleBuilderOptions<T, string?> EmailOpcional<T>(this IRuleBuilder<T, string?> regra) =>
        regra
            .Must(email => string.IsNullOrWhiteSpace(email) || (email.Trim().Length <= 150 && EmailValido.IsValid(email.Trim())))
            .WithMessage("Informe um e-mail válido de até 150 caracteres.");

    private static readonly EmailAddressAttribute EmailValido = new();
}
