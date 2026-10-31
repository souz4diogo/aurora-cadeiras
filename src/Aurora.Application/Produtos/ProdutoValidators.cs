using Aurora.Application.Comum;
using Aurora.Domain.Enums;
using FluentValidation;

namespace Aurora.Application.Produtos;

public sealed class ProdutoRequestValidator : AbstractValidator<ProdutoRequest>
{
    public ProdutoRequestValidator()
    {
        RuleFor(x => x.Codigo).CodigoDeCadastro();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(120);
        RuleFor(x => x.LinhaProdutoId).Must(l => Enum.IsDefined((LinhaProduto)l)).WithMessage("Linha de produto inválida.");
        RuleFor(x => x.PrecoVenda).GreaterThanOrEqualTo(0).PrecisionScale(12, 2, ignoreTrailingZeros: true);
    }
}
