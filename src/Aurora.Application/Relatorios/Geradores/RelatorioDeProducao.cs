using Aurora.Application.Abstracoes;
using Aurora.Domain.Enums;

namespace Aurora.Application.Relatorios.Geradores;

/// <summary>Ordens de produção em aberto, cada uma na estação em que está agora (a mesma leitura do quadro de produção).</summary>
public sealed class RelatorioDeProducao(IRelatoriosRepository relatorios) : IGeradorDeRelatorio
{
    public string Tipo => "producao";

    public string Titulo => "Relatório de produção";

    public FiltrosDoRelatorio Filtros => FiltrosDoRelatorio.Nenhum;

    public IReadOnlySet<PerfilUsuario> Perfis { get; } =
        new HashSet<PerfilUsuario> { PerfilUsuario.Administrador, PerfilUsuario.GerenteProducao };

    public async Task<Relatorio> GerarAsync(FiltroDeRelatorio filtro, DateTime agoraUtc, CancellationToken ct = default)
    {
        var resultado = await relatorios.ListarProducaoAsync(ConstantesDeRelatorio.LimiteDeLinhas, ct);
        var linhas = resultado.Itens
            .Select(o => (IReadOnlyList<object?>)
            [
                o.NumeroOp,
                o.Produto,
                o.Quantidade,
                o.EstacaoAtual,
                o.StatusDaEtapa,
                o.MinutosNaEtapa,
                o.PrevisaoConclusao,
                o.Atrasada ? "Sim" : "Não",
            ])
            .ToList();

        var secao = new SecaoDeRelatorio(
            "Ordens de produção em aberto",
            [
                new("Ordem"),
                new("Produto"),
                new("Quantidade", TipoDeColuna.Inteiro),
                new("Estação atual"),
                new("Situação da etapa"),
                new("Minutos na etapa", TipoDeColuna.Inteiro),
                new("Previsão de conclusão", TipoDeColuna.Data),
                new("Atrasada"),
            ],
            linhas,
            Totais: ["Total", null, resultado.Itens.Sum(o => o.Quantidade), null, null, null, null, $"{resultado.Itens.Count(o => o.Atrasada)} atrasadas"],
            Aviso: ConstantesDeRelatorio.AvisoDeCorte(resultado.Itens.Count, resultado.Total));
        return new Relatorio(Titulo, agoraUtc, ["Situação atual da fábrica"], [secao]);
    }
}
