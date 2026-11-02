using Aurora.Application.Abstracoes;
using Aurora.Application.Qualidade;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

public sealed class QualidadeRepository(AuroraDbContext db) : IQualidadeRepository
{
    public Task RegistrarInspecaoAsync(
        int ordemProducaoId,
        ResultadoInspecao resultado,
        byte? estacaoRetrabalhoId,
        string? observacao,
        CancellationToken ct = default)
    {
        var nome = resultado.ToString();
        return db.ExecutarProcedureAsync(
            $"EXEC dbo.sp_RegistrarInspecao @OrdemProducaoId = {ordemProducaoId}, @Resultado = {nome}, @EstacaoRetrabalhoId = {estacaoRetrabalhoId}, @Observacao = {observacao}",
            ct);
    }

    public Task<InspecaoResumo?> ObterUltimaDaOrdemAsync(int ordemProducaoId, CancellationToken ct = default) =>
        Consultar(db.Inspecoes.AsNoTracking().Where(i => i.OrdemProducaoId == ordemProducaoId))
            .Take(1)
            .Cast<InspecaoResumo?>()
            .FirstOrDefaultAsync(ct);

    public async Task<(IReadOnlyList<InspecaoResumo> Itens, int Total)> ListarAsync(
        FiltroDeInspecoes filtro, int pagina, int tamanho, CancellationToken ct = default)
    {
        var inspecoes = db.Inspecoes.AsNoTracking();

        if (filtro.OrdemProducaoId is int ordemId)
        {
            inspecoes = inspecoes.Where(i => i.OrdemProducaoId == ordemId);
        }

        if (filtro.Resultado is ResultadoInspecao resultado)
        {
            inspecoes = inspecoes.Where(i => i.Resultado == resultado);
        }

        if (filtro.De is DateOnly de)
        {
            var inicio = de.ToDateTime(TimeOnly.MinValue);
            inspecoes = inspecoes.Where(i => i.DataHora >= inicio);
        }

        if (filtro.Ate is DateOnly ate)
        {
            var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            inspecoes = inspecoes.Where(i => i.DataHora < fim);
        }

        var total = await inspecoes.CountAsync(ct);
        var itens = await Consultar(inspecoes).Skip((pagina - 1) * tamanho).Take(tamanho).ToListAsync(ct);
        return (itens, total);
    }

    /// <summary>Inspeções com a OP, o pedido, o produto, o inspetor e a estação de retrabalho, da mais recente à mais antiga.</summary>
    private IQueryable<InspecaoResumo> Consultar(IQueryable<Inspecao> inspecoes) =>
        from i in inspecoes
        join o in db.OrdensProducao on i.OrdemProducaoId equals o.Id
        join pi in db.Set<PedidoItem>() on o.PedidoItemId equals pi.Id
        join pd in db.Pedidos on pi.PedidoId equals pd.Id
        join p in db.Produtos on o.ProdutoId equals p.Id
        join u in db.Usuarios on i.InspetorId equals u.Id
        join es in db.Estacoes on i.EstacaoRetrabalhoId equals (byte?)es.Id into retrabalhos
        from es in retrabalhos.DefaultIfEmpty()
        orderby i.DataHora descending, i.Id descending
        select new InspecaoResumo(
            i.Id,
            o.Id,
            o.Numero,
            pd.Numero,
            p.Nome,
            i.Resultado,
            i.EstacaoRetrabalhoId,
            es != null ? es.Nome : null,
            i.Observacao,
            u.Id,
            u.Nome,
            i.DataHora,
            o.Status,
            pd.Status);
}
