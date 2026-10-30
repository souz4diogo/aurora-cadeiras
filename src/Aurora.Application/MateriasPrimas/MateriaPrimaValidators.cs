using Aurora.Application.Comum;
using Aurora.Domain.Enums;
using FluentValidation;

namespace Aurora.Application.MateriasPrimas;

public sealed class MateriaPrimaRequestValidator : AbstractValidator<MateriaPrimaRequest>
{
    public MateriaPrimaRequestValidator()
    {
        RuleFor(x => x.Codigo).CodigoDeCadastro();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Unidade).Must(u => UnidadesDeMedida.TentarObter(u, out _))
            .WithMessage("Unidade inválida. Use: un, m, m2, m3 ou l.");
        RuleFor(x => x.EstoqueMinimo).GreaterThanOrEqualTo(0).PrecisionScale(12, 3, ignoreTrailingZeros: true);
        RuleFor(x => x.CustoUnitario).GreaterThanOrEqualTo(0).PrecisionScale(12, 4, ignoreTrailingZeros: true);
        RuleFor(x => x.FornecedorPreferencialId).GreaterThan(0).When(x => x.FornecedorPreferencialId is not null);
    }
}
