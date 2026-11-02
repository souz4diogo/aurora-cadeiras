using Aurora.Application.Abstracoes;
using Aurora.Application.Comum;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using FluentValidation;

namespace Aurora.Application.Producao;

public sealed class ApontamentoRequestValidator : AbstractValidator<ApontamentoRequest>
{
    /// <summary>Folga para o relógio do aparelho estar adiantado em relação ao servidor.</summary>
    public static readonly TimeSpan FolgaDeRelogio = TimeSpan.FromMinutes(10);

    public ApontamentoRequestValidator(IRelogio relogio)
    {
        RuleFor(x => x.ClientRequestId).NotEqual(Guid.Empty).WithMessage("Informe o identificador da requisição.");
        RuleFor(x => x.DataHoraCliente)
            .Must(h => h.Year >= 2000).WithMessage("Informe a data e hora da ação.")
            .Must(h => h.UtcDateTime <= relogio.AgoraUtc + FolgaDeRelogio).WithMessage("A data e hora da ação está no futuro.");
    }
}

public sealed class SincronizacaoRequestValidator : AbstractValidator<SincronizacaoRequest>
{
    /// <summary>Tamanho máximo do lote; a fila local maior que isso é enviada em mais de uma chamada.</summary>
    public const int TamanhoMaximoDoLote = 200;

    public SincronizacaoRequestValidator()
    {
        RuleFor(x => x.Itens).NotNull().WithMessage("Informe os itens do lote.")
            .Must(i => i is null || i.Count > 0).WithMessage("O lote não pode ser vazio.")
            .Must(i => i is null || i.Count <= TamanhoMaximoDoLote)
            .WithMessage($"O lote aceita no máximo {TamanhoMaximoDoLote} itens.");
    }
}

public sealed class PerdaRequestValidator : AbstractValidator<PerdaRequest>
{
    public const int QuantidadeMaxima = 100_000;

    public PerdaRequestValidator()
    {
        RuleFor(x => x.Tipo).NotEmpty().Must(t => EnumPorNome.TentarObter<TipoPerda>(t, out _))
            .WithMessage("O tipo da perda deve ser Refugo ou Retrabalho.");
        RuleFor(x => x.Quantidade).InclusiveBetween(1, QuantidadeMaxima);
        RuleFor(x => x.Motivo).NotEmpty().WithMessage("O motivo da perda é obrigatório.")
            .MaximumLength(RegistroPerda.TamanhoMaximoDoMotivo);
    }
}
