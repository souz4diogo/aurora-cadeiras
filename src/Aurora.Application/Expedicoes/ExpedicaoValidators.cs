using Aurora.Domain.Entidades;
using FluentValidation;

namespace Aurora.Application.Expedicoes;

public sealed class ExpedirRequestValidator : AbstractValidator<ExpedirRequest>
{
    public ExpedirRequestValidator()
    {
        RuleFor(x => x.Transportadora).MaximumLength(Expedicao.TamanhoMaximoDaTransportadora);
        RuleFor(x => x.CodigoRastreio).MaximumLength(Expedicao.TamanhoMaximoDoCodigoDeRastreio);
    }
}
