using Aurora.Application.Abstracoes;
using FluentValidation;

namespace Aurora.Application.Compras;

public sealed class PedidoCompraItemRequestValidator : AbstractValidator<PedidoCompraItemRequest>
{
    // Limites das colunas decimal(12,3) e decimal(12,4): um valor maior estouraria o banco em vez de dar erro claro
    public const decimal QuantidadeMaxima = 999_999_999.999m;
    public const decimal CustoMaximo = 99_999_999.9999m;

    public PedidoCompraItemRequestValidator()
    {
        RuleFor(x => x.MateriaPrimaId).GreaterThan(0);
        RuleFor(x => x.Quantidade).GreaterThan(0).LessThanOrEqualTo(QuantidadeMaxima)
            .PrecisionScale(12, 3, ignoreTrailingZeros: true).WithMessage("A quantidade aceita no máximo 3 casas decimais.");
        RuleFor(x => x.CustoUnitario).GreaterThanOrEqualTo(0).LessThanOrEqualTo(CustoMaximo)
            .PrecisionScale(12, 4, ignoreTrailingZeros: true).WithMessage("O custo aceita no máximo 4 casas decimais.")
            .When(x => x.CustoUnitario is not null);
    }
}

public sealed class PedidoCompraRequestValidator : AbstractValidator<PedidoCompraRequest>
{
    public const int MaximoDeItens = 100;

    public PedidoCompraRequestValidator(IRelogio relogio)
    {
        RuleFor(x => x.FornecedorId).GreaterThan(0);

        // Um dia de folga: o servidor trabalha em UTC e o usuário no horário de Brasília
        RuleFor(x => x.DataPrevista)
            .Must(data => data is null || data >= DateOnly.FromDateTime(relogio.AgoraUtc).AddDays(-1))
            .WithMessage("A data prevista não pode estar no passado.");

        RuleFor(x => x.Itens).NotNull().NotEmpty().WithMessage("O pedido de compra precisa de ao menos um item.")
            .Must(itens => itens.Count <= MaximoDeItens).WithMessage($"O pedido aceita no máximo {MaximoDeItens} itens.")
            .Must(SemMateriaRepetida).WithMessage("A mesma matéria-prima não pode aparecer duas vezes; some as quantidades.");
        RuleForEach(x => x.Itens).NotNull().SetValidator(new PedidoCompraItemRequestValidator());
    }

    private static bool SemMateriaRepetida(IReadOnlyList<PedidoCompraItemRequest> itens) =>
        itens.Where(i => i is not null).Select(i => i.MateriaPrimaId).Distinct().Count() == itens.Count(i => i is not null);
}

public sealed class ReceberItemRequestValidator : AbstractValidator<ReceberItemRequest>
{
    public ReceberItemRequestValidator()
    {
        RuleFor(x => x.Quantidade).GreaterThan(0).LessThanOrEqualTo(PedidoCompraItemRequestValidator.QuantidadeMaxima)
            .PrecisionScale(12, 3, ignoreTrailingZeros: true).WithMessage("A quantidade aceita no máximo 3 casas decimais.");
    }
}
