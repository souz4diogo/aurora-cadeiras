using FluentValidation;

namespace Aurora.Application.FichasTecnicas;

public sealed class FichaTecnicaItemRequestValidator : AbstractValidator<FichaTecnicaItemRequest>
{
    public FichaTecnicaItemRequestValidator()
    {
        RuleFor(x => x.MateriaPrimaId).GreaterThan(0);
        RuleFor(x => x.EstacaoId).GreaterThan((byte)0);
        RuleFor(x => x.Quantidade).GreaterThan(0).PrecisionScale(12, 3, ignoreTrailingZeros: true);
    }
}

public sealed class FichaTecnicaRequestValidator : AbstractValidator<FichaTecnicaRequest>
{
    public const int MaximoDeItens = 200;

    public FichaTecnicaRequestValidator()
    {
        RuleFor(x => x.Itens).NotNull()
            .Must(itens => itens.Count <= MaximoDeItens).WithMessage($"A ficha aceita no máximo {MaximoDeItens} itens.")
            .Must(SemRepeticao).WithMessage("A mesma matéria-prima não pode aparecer duas vezes na mesma estação.");
        RuleForEach(x => x.Itens).NotNull().SetValidator(new FichaTecnicaItemRequestValidator());
    }

    private static bool SemRepeticao(IReadOnlyList<FichaTecnicaItemRequest> itens) =>
        itens.Where(i => i is not null).Select(i => (i.MateriaPrimaId, i.EstacaoId)).Distinct().Count()
        == itens.Count(i => i is not null);
}
