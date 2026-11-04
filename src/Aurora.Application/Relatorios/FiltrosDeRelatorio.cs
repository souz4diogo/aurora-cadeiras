using Aurora.Application.Comum;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;

namespace Aurora.Application.Relatorios;

/// <summary>Validação dos filtros que vários relatórios compartilham.</summary>
public static class FiltrosDeRelatorio
{
    public static void ValidarPeriodo(DateOnly? de, DateOnly? ate)
    {
        if (de is not null && ate is not null && de > ate)
        {
            throw new RegraDeNegocioException(
                "FILTRO_INVALIDO", "A data inicial não pode ser depois da data final.", TipoErro.Validacao);
        }
    }

    /// <summary>Período padrão dos relatórios de consumo: os últimos 30 dias, hoje incluído.</summary>
    public static (DateOnly De, DateOnly Ate) PeriodoOuPadrao(DateOnly? de, DateOnly? ate, DateTime agoraUtc, int diasPadrao = 30)
    {
        ValidarPeriodo(de, ate);
        var fim = ate ?? DiaDeNegocio.De(agoraUtc);
        return (de ?? fim.AddDays(1 - diasPadrao), fim);
    }
}

/// <summary>Textos em português das situações, como aparecem nos relatórios e na tela.</summary>
public static class RotulosDeSituacao
{
    public static string De(StatusPedido status) => status switch
    {
        StatusPedido.Aberto => "Aberto",
        StatusPedido.Confirmado => "Confirmado",
        StatusPedido.EmProducao => "Em produção",
        StatusPedido.Pronto => "Pronto",
        StatusPedido.Expedido => "Expedido",
        StatusPedido.Cancelado => "Cancelado",
        _ => status.ToString(),
    };
}
