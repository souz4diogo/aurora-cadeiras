using Aurora.Domain.Enums;
using FluentValidation;

namespace Aurora.Application.Usuarios;

/// <summary>Regras comuns de criação e atualização de usuário.</summary>
public abstract class DadosDoUsuarioValidator<T> : AbstractValidator<T>
    where T : IDadosDoUsuario
{
    protected DadosDoUsuarioValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.PerfilId).Must(p => Enum.IsDefined((PerfilUsuario)p)).WithMessage("Perfil inválido.");
        RuleFor(x => x.ClienteId)
            .NotNull().When(x => x.PerfilId == (byte)PerfilUsuario.Cliente)
            .WithMessage("O perfil Cliente exige o clienteId.");
    }
}

public sealed class CriarUsuarioRequestValidator : DadosDoUsuarioValidator<CriarUsuarioRequest>;

public sealed class AtualizarUsuarioRequestValidator : DadosDoUsuarioValidator<AtualizarUsuarioRequest>;
