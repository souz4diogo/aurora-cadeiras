using Aurora.Application.Comum;
using FluentValidation;

namespace Aurora.Application.Clientes;

public sealed class ClienteRequestValidator : AbstractValidator<ClienteRequest>
{
    public ClienteRequestValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CpfCnpj).DocumentoComTamanho(11, 14);
        RuleFor(x => x.Email).EmailOpcional();
        RuleFor(x => x.Telefone).MaximumLength(20);
        RuleFor(x => x.Cidade).MaximumLength(80);
        RuleFor(x => x.Uf).Matches("^[A-Za-z]{2}$").WithMessage("A UF deve ter 2 letras.")
            .When(x => !string.IsNullOrWhiteSpace(x.Uf));
    }
}
