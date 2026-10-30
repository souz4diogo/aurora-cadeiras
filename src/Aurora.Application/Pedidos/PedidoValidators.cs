using Aurora.Application.Abstracoes;
using FluentValidation;

namespace Aurora.Application.Pedidos;

public sealed class PedidoItemRequestValidator : AbstractValidator<PedidoItemRequest>
{
    public const int QuantidadeMaxima = 100_000;

    public PedidoItemRequestValidator()
    {
        RuleFor(x => x.ProdutoId).GreaterThan(0);
        RuleFor(x => x.Quantidade).InclusiveBetween(1, QuantidadeMaxima);
    }
}

public sealed class PedidoRequestValidator : AbstractValidator<PedidoRequest>
{
    public const int MaximoDeItens = 100;

    public PedidoRequestValidator(IRelogio relogio)
    {
        RuleFor(x => x.ClienteId).GreaterThan(0);

        // Um dia de folga: o servidor trabalha em UTC e o usuário no horário de Brasília
        RuleFor(x => x.PrazoEntrega)
            .Must(prazo => prazo >= DateOnly.FromDateTime(relogio.AgoraUtc).AddDays(-1))
            .WithMessage("O prazo de entrega não pode estar no passado.");

        RuleFor(x => x.Observacao).MaximumLength(300);

        RuleFor(x => x.Itens).NotNull().NotEmpty().WithMessage("O pedido precisa de ao menos um item.")
            .Must(itens => itens.Count <= MaximoDeItens).WithMessage($"O pedido aceita no máximo {MaximoDeItens} itens.")
            .Must(SemProdutoRepetido).WithMessage("O mesmo produto não pode aparecer duas vezes; some as quantidades.");
        RuleForEach(x => x.Itens).NotNull().SetValidator(new PedidoItemRequestValidator());
    }

    private static bool SemProdutoRepetido(IReadOnlyList<PedidoItemRequest> itens) =>
        itens.Where(i => i is not null).Select(i => i.ProdutoId).Distinct().Count() == itens.Count(i => i is not null);
}
