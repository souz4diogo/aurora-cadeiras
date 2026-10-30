using FluentValidation;

namespace Aurora.Application.Estoque;

public sealed class AjusteDeEstoqueRequestValidator : AbstractValidator<AjusteDeEstoqueRequest>
{
    public const int TamanhoMaximoDoMotivo = 200;

    public AjusteDeEstoqueRequestValidator()
    {
        RuleFor(x => x.MateriaPrimaId).GreaterThan(0);
        RuleFor(x => x.NovaQuantidade).GreaterThanOrEqualTo(0).PrecisionScale(12, 3, ignoreTrailingZeros: true);
        RuleFor(x => x.Motivo).NotEmpty().WithMessage("O motivo do ajuste é obrigatório.")
            .MaximumLength(TamanhoMaximoDoMotivo);
    }
}
