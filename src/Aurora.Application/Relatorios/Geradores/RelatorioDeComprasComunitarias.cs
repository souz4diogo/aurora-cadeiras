using System.Globalization;
using Aurora.Application.Abstracoes;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;

namespace Aurora.Application.Relatorios.Geradores;

/// <summary>
/// Compras recebidas por mês e a participação de fornecedores de comunidades tradicionais: o indicador de impacto
/// social da Etapa 3 (valorização afro-indígena e remuneração justa).
/// </summary>
public sealed class RelatorioDeComprasComunitarias(IRelatoriosRepository relatorios) : IGeradorDeRelatorio
{
    public string Tipo => "compras-comunitarias";

    public string Titulo => "Compras de comunidades tradicionais";

    public FiltrosDoRelatorio Filtros => FiltrosDoRelatorio.Ano;

    public IReadOnlySet<PerfilUsuario> Perfis { get; } =
        new HashSet<PerfilUsuario> { PerfilUsuario.Administrador, PerfilUsuario.GerenteProducao };

    public async Task<Relatorio> GerarAsync(FiltroDeRelatorio filtro, DateTime agoraUtc, CancellationToken ct = default)
    {
        if (filtro.Ano is < 2000 or > 2100)
        {
            throw new RegraDeNegocioException("FILTRO_INVALIDO", "Informe um ano entre 2000 e 2100.", TipoErro.Validacao);
        }

        var meses = await relatorios.ListarComprasComunitariasAsync(filtro.Ano, ct);
        var totalComunidades = meses.Sum(m => m.ValorComunidades);
        var total = meses.Sum(m => m.ValorTotal);

        var secao = new SecaoDeRelatorio(
            "Recebimentos por mês",
            [
                new("Mês"),
                new("Comunidades tradicionais", TipoDeColuna.Moeda),
                new("Total comprado", TipoDeColuna.Moeda),
                new("Participação das comunidades", TipoDeColuna.Percentual),
            ],
            meses.Select(m => (IReadOnlyList<object?>)[Mes(m.Ano, m.Mes), m.ValorComunidades, m.ValorTotal, m.Percentual]).ToList(),
            Totais: ["Total", totalComunidades, total, total == 0 ? 0m : Math.Round(100m * totalComunidades / total, 2)]);

        var filtros = new List<string> { filtro.Ano is int ano ? $"Ano: {ano}" : "Ano: todos" };
        return new Relatorio(Titulo, agoraUtc, filtros, [secao]);
    }

    private static string Mes(int ano, int mes) =>
        $"{CultureInfo.GetCultureInfo("pt-BR").DateTimeFormat.GetMonthName(mes)} de {ano}";
}
