using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using FluentValidation;

namespace Aurora.Application.Qualidade;

public sealed class InspecaoRequestValidator : AbstractValidator<InspecaoRequest>
{
    // A exigência da estação de retrabalho na reprovação fica na procedure (RN06, 422): uma regra, um lugar
    public InspecaoRequestValidator()
    {
        RuleFor(x => x.Resultado).NotEmpty().Must(r => TentarObterResultado(r, out _))
            .WithMessage("O resultado da inspeção deve ser Aprovada ou Reprovada.");
        RuleFor(x => x.Observacao).MaximumLength(Inspecao.TamanhoMaximoDaObservacao);
    }

    /// <summary>Aceita só o nome do resultado (sem diferenciar maiúsculas); o Enum.TryParse sozinho aceitaria "1".</summary>
    public static bool TentarObterResultado(string? texto, out ResultadoInspecao resultado)
    {
        var nome = Enum.GetNames<ResultadoInspecao>()
            .FirstOrDefault(n => string.Equals(n, texto?.Trim(), StringComparison.OrdinalIgnoreCase));
        resultado = nome is null ? default : Enum.Parse<ResultadoInspecao>(nome);
        return nome is not null;
    }
}
