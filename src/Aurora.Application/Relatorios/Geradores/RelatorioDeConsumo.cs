using Aurora.Application.Abstracoes;
using Aurora.Domain.Enums;

namespace Aurora.Application.Relatorios.Geradores;

/// <summary>Matéria-prima que a produção consumiu no período, por material e por produto (vw_ConsumoMateriaPrima).</summary>
public sealed class RelatorioDeConsumo(IRelatoriosRepository relatorios) : IGeradorDeRelatorio
{
    public string Tipo => "consumo";

    public string Titulo => "Relatório de consumo de matéria-prima";

    public FiltrosDoRelatorio Filtros => FiltrosDoRelatorio.Periodo;

    public IReadOnlySet<PerfilUsuario> Perfis { get; } =
        new HashSet<PerfilUsuario> { PerfilUsuario.Administrador, PerfilUsuario.GerenteProducao };

    public async Task<Relatorio> GerarAsync(FiltroDeRelatorio filtro, DateTime agoraUtc, CancellationToken ct = default)
    {
        var (de, ate) = FiltrosDeRelatorio.PeriodoOuPadrao(filtro.De, filtro.Ate, agoraUtc);
        var porMateria = await relatorios.ListarConsumoPorMateriaAsync(de, ate, ConstantesDeRelatorio.LimiteDeLinhas, ct);
        var porProduto = await relatorios.ListarConsumoPorProdutoAsync(de, ate, ConstantesDeRelatorio.LimiteDeLinhas, ct);

        var secaoDeMaterias = new SecaoDeRelatorio(
            "Por matéria-prima",
            [new("Código"), new("Material"), new("Unidade"), new("Quantidade consumida", TipoDeColuna.Quantidade), new("Custo estimado", TipoDeColuna.Moeda)],
            porMateria.Select(m => (IReadOnlyList<object?>)[m.Codigo, m.Nome, m.Unidade, m.Quantidade, m.CustoEstimado]).ToList(),
            Totais: ["Total", null, null, null, porMateria.Sum(m => m.CustoEstimado)]);

        var secaoDeProdutos = new SecaoDeRelatorio(
            "Custo por produto",
            [new("Código"), new("Produto"), new("Custo estimado", TipoDeColuna.Moeda)],
            porProduto.Select(p => (IReadOnlyList<object?>)[p.Codigo, p.Nome, p.CustoEstimado]).ToList(),
            Totais: ["Total", null, porProduto.Sum(p => p.CustoEstimado)]);

        return new Relatorio(Titulo, agoraUtc, [ConstantesDeRelatorio.Periodo(de, ate)], [secaoDeMaterias, secaoDeProdutos]);
    }
}
