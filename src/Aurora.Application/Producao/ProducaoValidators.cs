using Aurora.Application.Abstracoes;
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

public sealed class PerdaRequestValidator : AbstractValidator<PerdaRequest>
{
    public const int QuantidadeMaxima = 100_000;

    public PerdaRequestValidator()
    {
        RuleFor(x => x.Tipo).NotEmpty().Must(EhTipoConhecido)
            .WithMessage("O tipo da perda deve ser Refugo ou Retrabalho.");
        RuleFor(x => x.Quantidade).InclusiveBetween(1, QuantidadeMaxima);
        RuleFor(x => x.Motivo).NotEmpty().WithMessage("O motivo da perda é obrigatório.")
            .MaximumLength(RegistroPerda.TamanhoMaximoDoMotivo);
    }

    public static bool EhTipoConhecido(string? tipo) => TentarObterTipo(tipo, out _);

    /// <summary>Aceita só o nome do tipo (sem diferenciar maiúsculas); o Enum.TryParse sozinho aceitaria "1".</summary>
    public static bool TentarObterTipo(string? texto, out TipoPerda tipo)
    {
        var nome = Enum.GetNames<TipoPerda>()
            .FirstOrDefault(n => string.Equals(n, texto?.Trim(), StringComparison.OrdinalIgnoreCase));
        tipo = nome is null ? default : Enum.Parse<TipoPerda>(nome);
        return nome is not null;
    }
}
