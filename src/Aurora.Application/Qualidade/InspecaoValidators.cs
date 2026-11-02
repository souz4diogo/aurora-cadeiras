using Aurora.Application.Comum;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using FluentValidation;

namespace Aurora.Application.Qualidade;

public sealed class InspecaoRequestValidator : AbstractValidator<InspecaoRequest>
{
    // A exigência da estação de retrabalho na reprovação fica na procedure (RN06, 422): uma regra, um lugar
    public InspecaoRequestValidator()
    {
        RuleFor(x => x.Resultado).NotEmpty().Must(r => EnumPorNome.TentarObter<ResultadoInspecao>(r, out _))
            .WithMessage("O resultado da inspeção deve ser Aprovada ou Reprovada.");
        RuleFor(x => x.Observacao).MaximumLength(Inspecao.TamanhoMaximoDaObservacao);
    }
}
