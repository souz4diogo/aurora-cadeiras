using Aurora.Domain.Enums;
using FluentValidation;

namespace Aurora.Application.Usuarios;

public sealed class CriarUsuarioRequestValidator : AbstractValidator<CriarUsuarioRequest>
{
    public CriarUsuarioRequestValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.PerfilId).Must(p => Enum.IsDefined((PerfilUsuario)p)).WithMessage("Perfil inválido.");
        RuleFor(x => x.ClienteId)
            .NotNull().When(x => x.PerfilId == (byte)PerfilUsuario.Cliente)
            .WithMessage("O perfil Cliente exige o clienteId.");
    }
}

public sealed class AtualizarUsuarioRequestValidator : AbstractValidator<AtualizarUsuarioRequest>
{
    public AtualizarUsuarioRequestValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.PerfilId).Must(p => Enum.IsDefined((PerfilUsuario)p)).WithMessage("Perfil inválido.");
        RuleFor(x => x.ClienteId)
            .NotNull().When(x => x.PerfilId == (byte)PerfilUsuario.Cliente)
            .WithMessage("O perfil Cliente exige o clienteId.");
    }
}
