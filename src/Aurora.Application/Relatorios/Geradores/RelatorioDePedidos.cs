using Aurora.Application.Abstracoes;
using Aurora.Application.Comum;
using Aurora.Domain.Enums;
using Aurora.Domain.Excecoes;

namespace Aurora.Application.Relatorios.Geradores;

/// <summary>Pedidos de venda no período, com situação e atraso. O vendedor recebe só os próprios pedidos (A25).</summary>
public sealed class RelatorioDePedidos(IRelatoriosRepository relatorios, ICurrentUser usuarioAtual) : IGeradorDeRelatorio
{
    public string Tipo => "pedidos";

    public string Titulo => "Relatório de pedidos";

    public FiltrosDoRelatorio Filtros => FiltrosDoRelatorio.Periodo | FiltrosDoRelatorio.Status;

    public IReadOnlySet<PerfilUsuario> Perfis { get; } =
        new HashSet<PerfilUsuario> { PerfilUsuario.Administrador, PerfilUsuario.GerenteProducao, PerfilUsuario.Vendedor };

    public async Task<Relatorio> GerarAsync(FiltroDeRelatorio filtro, DateTime agoraUtc, CancellationToken ct = default)
    {
        FiltrosDeRelatorio.ValidarPeriodo(filtro.De, filtro.Ate);
        StatusPedido? status = null;
        if (!string.IsNullOrWhiteSpace(filtro.Status))
        {
            status = EnumPorNome.TentarObter(filtro.Status, out StatusPedido obtido)
                ? obtido
                : throw new RegraDeNegocioException(
                    "FILTRO_INVALIDO", $"Situação desconhecida: {filtro.Status}.", TipoErro.Validacao);
        }

        var somenteDoVendedor = usuarioAtual.Perfil == PerfilUsuario.Vendedor ? usuarioAtual.UsuarioId : null;
        var resultado = await relatorios.ListarPedidosAsync(
            somenteDoVendedor, status, filtro.De, filtro.Ate, ConstantesDeRelatorio.LimiteDeLinhas, ct);

        var hoje = DateOnly.FromDateTime(agoraUtc);
        var linhas = resultado.Itens
            .Select(p => (IReadOnlyList<object?>)
            [
                p.Numero,
                p.Cliente,
                p.Vendedor,
                p.DataPedido,
                p.PrazoEntrega,
                RotulosDeSituacao.De(p.Status),
                p.Itens,
                p.ValorTotal,
                DiasDeAtraso(p.Status, p.PrazoEntrega, hoje),
            ])
            .ToList();

        var filtros = new List<string> { ConstantesDeRelatorio.Periodo(filtro.De, filtro.Ate) };
        if (status is not null)
        {
            filtros.Add($"Situação: {RotulosDeSituacao.De(status.Value)}");
        }

        if (somenteDoVendedor is not null)
        {
            filtros.Add("Somente os seus pedidos");
        }

        var secao = new SecaoDeRelatorio(
            "Pedidos",
            [
                new("Número"),
                new("Cliente"),
                new("Vendedor"),
                new("Data do pedido", TipoDeColuna.DataHora),
                new("Prazo de entrega", TipoDeColuna.Data),
                new("Situação"),
                new("Itens", TipoDeColuna.Inteiro),
                new("Valor total", TipoDeColuna.Moeda),
                new("Dias de atraso", TipoDeColuna.Inteiro),
            ],
            linhas,
            Totais: ["Total", null, null, null, null, null, resultado.Itens.Sum(p => p.Itens), resultado.Itens.Sum(p => p.ValorTotal), null],
            Aviso: ConstantesDeRelatorio.AvisoDeCorte(resultado.Itens.Count, resultado.Total));
        return new Relatorio(Titulo, agoraUtc, filtros, [secao]);
    }

    /// <summary>Mesma regra da view vw_PedidosAtrasados: em andamento e com o prazo vencido.</summary>
    private static int DiasDeAtraso(StatusPedido status, DateOnly prazo, DateOnly hoje) =>
        status is StatusPedido.Confirmado or StatusPedido.EmProducao or StatusPedido.Pronto && prazo < hoje
            ? hoje.DayNumber - prazo.DayNumber
            : 0;
}
