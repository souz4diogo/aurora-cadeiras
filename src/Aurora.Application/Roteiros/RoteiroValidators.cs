using FluentValidation;

namespace Aurora.Application.Roteiros;

public sealed class RoteiroRequestValidator : AbstractValidator<RoteiroRequest>
{
    public const int MaximoDeEstacoes = 10;

    public RoteiroRequestValidator()
    {
        RuleFor(x => x.Estacoes).NotNull().NotEmpty().WithMessage("O roteiro precisa de ao menos uma estação.")
            .Must(estacoes => estacoes.Count <= MaximoDeEstacoes).WithMessage($"O roteiro aceita no máximo {MaximoDeEstacoes} estações.")
            .Must(estacoes => estacoes.Distinct().Count() == estacoes.Count).WithMessage("Uma estação não pode aparecer duas vezes no roteiro.");
    }
}
