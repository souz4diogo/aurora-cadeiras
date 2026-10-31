using Aurora.Application.Comum;
using FluentValidation;

namespace Aurora.Application.Fornecedores;

public sealed class FornecedorRequestValidator : AbstractValidator<FornecedorRequest>
{
    public FornecedorRequestValidator()
    {
        RuleFor(x => x.RazaoSocial).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Cnpj).DocumentoComTamanho(14);
        RuleFor(x => x.Email).EmailOpcional();
        RuleFor(x => x.Telefone).MaximumLength(20);
    }
}
