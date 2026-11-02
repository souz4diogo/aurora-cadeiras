using Aurora.Application.Abstracoes;
using Aurora.Domain.Enums;

namespace Aurora.Application.Relatorios.Geradores;

/// <summary>Saldos de matéria-prima (com a situação crítica) e de produto acabado (total, reservado e disponível).</summary>
public sealed class RelatorioDeEstoque(IRelatoriosRepository relatorios) : IGeradorDeRelatorio
{
    public string Tipo => "estoque";

    public string Titulo => "Relatório de estoque";

    public FiltrosDoRelatorio Filtros => FiltrosDoRelatorio.Nenhum;

    public IReadOnlySet<PerfilUsuario> Perfis { get; } = new HashSet<PerfilUsuario>
    {
        PerfilUsuario.Administrador, PerfilUsuario.GerenteProducao, PerfilUsuario.Almoxarife,
    };

    public async Task<Relatorio> GerarAsync(FiltroDeRelatorio filtro, DateTime agoraUtc, CancellationToken ct = default)
    {
        var materias = await relatorios.ListarEstoqueDeMateriasAsync(ConstantesDeRelatorio.LimiteDeLinhas, ct);
        var produtos = await relatorios.ListarEstoqueDeProdutosAsync(ConstantesDeRelatorio.LimiteDeLinhas, ct);

        var secaoDeMaterias = new SecaoDeRelatorio(
            "Matérias-primas",
            [
                new("Código"),
                new("Material"),
                new("Unidade"),
                new("Saldo", TipoDeColuna.Quantidade),
                new("Estoque mínimo", TipoDeColuna.Quantidade),
                new("Situação"),
                new("Custo unitário", TipoDeColuna.Moeda),
                new("Valor em estoque", TipoDeColuna.Moeda),
            ],
            materias.Itens
                .Select(m => (IReadOnlyList<object?>)
                [
                    m.Codigo, m.Nome, m.Unidade, m.Saldo, m.Minimo, m.Critico ? "Crítico" : "Normal", m.CustoUnitario, m.ValorEmEstoque,
                ])
                .ToList(),
            Totais: ["Total", null, null, null, null, $"{materias.Itens.Count(m => m.Critico)} críticos", null, materias.Itens.Sum(m => m.ValorEmEstoque)],
            Aviso: ConstantesDeRelatorio.AvisoDeCorte(materias.Itens.Count, materias.Total));

        var secaoDeProdutos = new SecaoDeRelatorio(
            "Produto acabado",
            [
                new("Código"),
                new("Produto"),
                new("Quantidade", TipoDeColuna.Inteiro),
                new("Reservado", TipoDeColuna.Inteiro),
                new("Disponível", TipoDeColuna.Inteiro),
            ],
            produtos.Itens
                .Select(p => (IReadOnlyList<object?>)[p.Codigo, p.Nome, p.Quantidade, p.Reservado, p.Disponivel])
                .ToList(),
            Totais: ["Total", null, produtos.Itens.Sum(p => p.Quantidade), produtos.Itens.Sum(p => p.Reservado), produtos.Itens.Sum(p => p.Disponivel)],
            Aviso: ConstantesDeRelatorio.AvisoDeCorte(produtos.Itens.Count, produtos.Total));

        return new Relatorio(Titulo, agoraUtc, ["Saldos de agora"], [secaoDeMaterias, secaoDeProdutos]);
    }
}
