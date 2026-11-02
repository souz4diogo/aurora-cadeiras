using Aurora.Application.Painel;
using Aurora.Application.Relatorios;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

/// <summary>Consultas de consumo de matéria-prima sobre vw_ConsumoMateriaPrima, usadas pelo painel e pelo relatório.</summary>
internal static class ConsultasDeConsumo
{
    public static async Task<IReadOnlyList<ConsumoDeMaterialDto>> PorMateriaAsync(
        AuroraDbContext db, DateOnly de, DateOnly ate, int limite, CancellationToken ct)
    {
        var inicio = de.ToDateTime(TimeOnly.MinValue);
        var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var linhas = await db.Database.SqlQuery<LinhaDeMateria>($"""
            SELECT TOP ({limite})
                   MateriaPrimaId,
                   CodigoMateriaPrima AS Codigo,
                   MateriaPrima       AS Nome,
                   Unidade,
                   SUM(QuantidadeConsumida) AS Quantidade,
                   SUM(CustoEstimado)       AS CustoEstimado
              FROM dbo.vw_ConsumoMateriaPrima
             WHERE DataHora >= {inicio} AND DataHora < {fim}
             GROUP BY MateriaPrimaId, CodigoMateriaPrima, MateriaPrima, Unidade
             ORDER BY SUM(QuantidadeConsumida) DESC, MateriaPrimaId
            """).ToListAsync(ct);
        return linhas.Select(l => new ConsumoDeMaterialDto(l.MateriaPrimaId, l.Codigo, l.Nome, l.Unidade, l.Quantidade, l.CustoEstimado)).ToList();
    }

    public static async Task<IReadOnlyList<LinhaDeConsumoPorProduto>> PorProdutoAsync(
        AuroraDbContext db, DateOnly de, DateOnly ate, int limite, CancellationToken ct)
    {
        var inicio = de.ToDateTime(TimeOnly.MinValue);
        var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
        var linhas = await db.Database.SqlQuery<LinhaDeProduto>($"""
            SELECT TOP ({limite})
                   v.CodigoProduto AS Codigo,
                   p.Nome          AS Nome,
                   SUM(v.CustoEstimado) AS CustoEstimado
              FROM dbo.vw_ConsumoMateriaPrima v
              JOIN dbo.Produto p ON p.ProdutoId = v.ProdutoId
             WHERE v.DataHora >= {inicio} AND v.DataHora < {fim}
             GROUP BY v.CodigoProduto, p.Nome
             ORDER BY SUM(v.CustoEstimado) DESC, v.CodigoProduto
            """).ToListAsync(ct);
        return linhas.Select(l => new LinhaDeConsumoPorProduto(l.Codigo, l.Nome, l.CustoEstimado)).ToList();
    }

    // O SqlQuery preenche estas classes pelo nome das colunas
    private sealed class LinhaDeMateria
    {
        public int MateriaPrimaId { get; set; }

        public string Codigo { get; set; } = "";

        public string Nome { get; set; } = "";

        public string Unidade { get; set; } = "";

        public decimal Quantidade { get; set; }

        public decimal CustoEstimado { get; set; }
    }

    private sealed class LinhaDeProduto
    {
        public string Codigo { get; set; } = "";

        public string Nome { get; set; } = "";

        public decimal CustoEstimado { get; set; }
    }
}
