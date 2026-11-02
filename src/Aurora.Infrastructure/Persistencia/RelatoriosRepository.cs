using Aurora.Application.Abstracoes;
using Aurora.Application.Painel;
using Aurora.Application.Relatorios;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

/// <summary>Consultas dos relatórios: somente leitura, sempre com limite de linhas e o total para avisar quando corta.</summary>
public sealed class RelatoriosRepository(AuroraDbContext db) : IRelatoriosRepository
{
    public async Task<ResultadoLimitado<LinhaDePedido>> ListarPedidosAsync(
        int? vendedorId, StatusPedido? status, DateOnly? de, DateOnly? ate, int limite, CancellationToken ct = default)
    {
        var pedidos = db.Pedidos.AsNoTracking().AsQueryable();
        if (vendedorId is int vendedor)
        {
            pedidos = pedidos.Where(p => p.VendedorId == vendedor);
        }

        if (status is StatusPedido situacao)
        {
            pedidos = pedidos.Where(p => p.Status == situacao);
        }

        if (de is DateOnly inicio)
        {
            var desde = inicio.ToDateTime(TimeOnly.MinValue);
            pedidos = pedidos.Where(p => p.DataPedido >= desde);
        }

        if (ate is DateOnly fim)
        {
            var ateOndeVale = fim.AddDays(1).ToDateTime(TimeOnly.MinValue);
            pedidos = pedidos.Where(p => p.DataPedido < ateOndeVale);
        }

        var total = await pedidos.CountAsync(ct);
        var itens = await (
                from p in pedidos
                join c in db.Clientes on p.ClienteId equals c.Id
                join u in db.Usuarios on p.VendedorId equals u.Id
                orderby p.DataPedido descending, p.Id descending
                select new LinhaDePedido(
                    p.Numero, c.Nome, u.Nome, p.DataPedido, p.PrazoEntrega, p.Status, p.Itens.Count, p.ValorTotal))
            .Take(limite)
            .ToListAsync(ct);
        return new ResultadoLimitado<LinhaDePedido>(itens, total);
    }

    public async Task<ResultadoLimitado<LinhaDeProducao>> ListarProducaoAsync(int limite, CancellationToken ct = default)
    {
        // Cada OP uma vez só, na estação em que está agora (primeira etapa não concluída)
        var linhas = await db.Database.SqlQuery<LinhaDaOrdem>($"""
            WITH Atual AS (
              SELECT *, ROW_NUMBER() OVER (PARTITION BY OrdemProducaoId ORDER BY Sequencia) AS Posicao
                FROM dbo.vw_PainelProducao
            )
            SELECT TOP ({limite})
                   NumeroOp, Produto, Quantidade, Estacao, StatusEtapa, MinutosNaEtapa, PrevisaoConclusao,
                   CAST(Atrasada AS int) AS Atrasada
              FROM Atual
             WHERE Posicao = 1
             ORDER BY Atrasada DESC, PrevisaoConclusao, NumeroOp
            """).ToListAsync(ct);
        var total = await db.Database.SqlQuery<int>($"""
            SELECT COUNT(DISTINCT OrdemProducaoId) AS [Value] FROM dbo.vw_PainelProducao
            """).SingleAsync(ct);

        var itens = linhas
            .Select(l => new LinhaDeProducao(
                l.NumeroOp,
                l.Produto,
                l.Quantidade,
                l.Estacao,
                l.StatusEtapa == "EmAndamento" ? "Em andamento" : "Aguardando",
                l.MinutosNaEtapa,
                l.PrevisaoConclusao,
                l.Atrasada == 1))
            .ToList();
        return new ResultadoLimitado<LinhaDeProducao>(itens, total);
    }

    public async Task<ResultadoLimitado<LinhaDeEstoqueDeMateria>> ListarEstoqueDeMateriasAsync(int limite, CancellationToken ct = default)
    {
        var materias = db.MateriasPrimas.AsNoTracking().Where(m => m.Ativo);
        var total = await materias.CountAsync(ct);
        var lidas = await materias.OrderBy(m => m.Nome).ThenBy(m => m.Id).Take(limite).ToListAsync(ct);
        var itens = lidas
            .Select(m => new LinhaDeEstoqueDeMateria(
                m.Codigo, m.Nome, m.Unidade.ParaCodigo(), m.EstoqueAtual, m.EstoqueMinimo, m.CustoUnitario))
            .ToList();
        return new ResultadoLimitado<LinhaDeEstoqueDeMateria>(itens, total);
    }

    public async Task<ResultadoLimitado<LinhaDeEstoqueDeProduto>> ListarEstoqueDeProdutosAsync(int limite, CancellationToken ct = default)
    {
        var produtos = db.Produtos.AsNoTracking().Where(p => p.Ativo);
        var total = await produtos.CountAsync(ct);
        var itens = await (
                from p in produtos
                join e in db.EstoquesDeProduto on p.Id equals e.ProdutoId into estoques
                from e in estoques.DefaultIfEmpty()
                orderby p.Nome, p.Id
                select new LinhaDeEstoqueDeProduto(p.Codigo, p.Nome, e == null ? 0 : e.Quantidade, e == null ? 0 : e.Reservado))
            .Take(limite)
            .ToListAsync(ct);
        return new ResultadoLimitado<LinhaDeEstoqueDeProduto>(itens, total);
    }

    public Task<IReadOnlyList<ConsumoDeMaterialDto>> ListarConsumoPorMateriaAsync(
        DateOnly de, DateOnly ate, int limite, CancellationToken ct = default) =>
        ConsultasDeConsumo.PorMateriaAsync(db, de, ate, limite, ct);

    public Task<IReadOnlyList<LinhaDeConsumoPorProduto>> ListarConsumoPorProdutoAsync(
        DateOnly de, DateOnly ate, int limite, CancellationToken ct = default) =>
        ConsultasDeConsumo.PorProdutoAsync(db, de, ate, limite, ct);

    public async Task<IReadOnlyList<LinhaDeComprasComunitarias>> ListarComprasComunitariasAsync(int? ano, CancellationToken ct = default)
    {
        var linhas = await db.Database.SqlQuery<LinhaDoMes>($"""
            SELECT Ano, Mes, ValorComunidades, ValorTotal, PercentualComunidades AS Percentual
              FROM dbo.vw_ComprasComunidadesTradicionais
             WHERE ({ano} IS NULL OR Ano = {ano})
             ORDER BY Ano, Mes
            """).ToListAsync(ct);
        return linhas.Select(l => new LinhaDeComprasComunitarias(l.Ano, l.Mes, l.ValorComunidades, l.ValorTotal, l.Percentual)).ToList();
    }

    // O SqlQuery preenche estas classes pelo nome das colunas
    private sealed class LinhaDaOrdem
    {
        public string NumeroOp { get; set; } = "";

        public string Produto { get; set; } = "";

        public int Quantidade { get; set; }

        public string Estacao { get; set; } = "";

        public string StatusEtapa { get; set; } = "";

        public int? MinutosNaEtapa { get; set; }

        public DateOnly? PrevisaoConclusao { get; set; }

        public int Atrasada { get; set; }
    }

    private sealed class LinhaDoMes
    {
        public int Ano { get; set; }

        public int Mes { get; set; }

        public decimal ValorComunidades { get; set; }

        public decimal ValorTotal { get; set; }

        public decimal Percentual { get; set; }
    }
}
