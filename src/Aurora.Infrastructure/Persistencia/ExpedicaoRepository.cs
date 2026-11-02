using Aurora.Application.Abstracoes;
using Aurora.Application.Expedicoes;
using Aurora.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

public sealed class ExpedicaoRepository(AuroraDbContext db) : IExpedicaoRepository
{
    public Task ExpedirAsync(int pedidoId, string? transportadora, string? codigoRastreio, CancellationToken ct = default) =>
        db.ExecutarProcedureAsync(
            $"EXEC dbo.sp_Expedir @PedidoId = {pedidoId}, @Transportadora = {transportadora}, @CodigoRastreio = {codigoRastreio}",
            ct);

    public Task<ExpedicaoResumo?> ObterPorPedidoAsync(int pedidoId, CancellationToken ct = default) =>
        Consultar(db.Expedicoes.AsNoTracking().Where(x => x.PedidoId == pedidoId))
            .Cast<ExpedicaoResumo?>()
            .FirstOrDefaultAsync(ct);

    public async Task<(IReadOnlyList<ExpedicaoResumo> Itens, int Total)> ListarAsync(
        FiltroDeExpedicoes filtro, int pagina, int tamanho, CancellationToken ct = default)
    {
        var expedicoes = db.Expedicoes.AsNoTracking();

        if (filtro.VendedorId is int vendedorId)
        {
            expedicoes = expedicoes.Where(x => db.Pedidos.Any(pd => pd.Id == x.PedidoId && pd.VendedorId == vendedorId));
        }

        if (filtro.De is DateOnly de)
        {
            var inicio = de.ToDateTime(TimeOnly.MinValue);
            expedicoes = expedicoes.Where(x => x.DataHora >= inicio);
        }

        if (filtro.Ate is DateOnly ate)
        {
            var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            expedicoes = expedicoes.Where(x => x.DataHora < fim);
        }

        var total = await expedicoes.CountAsync(ct);
        var itens = await Consultar(expedicoes).Skip((pagina - 1) * tamanho).Take(tamanho).ToListAsync(ct);
        return (itens, total);
    }

    /// <summary>Expedições com o pedido, o cliente e o responsável, da mais recente à mais antiga.</summary>
    private IQueryable<ExpedicaoResumo> Consultar(IQueryable<Expedicao> expedicoes) =>
        from x in expedicoes
        join pd in db.Pedidos on x.PedidoId equals pd.Id
        join c in db.Clientes on pd.ClienteId equals c.Id
        join u in db.Usuarios on x.UsuarioId equals u.Id
        orderby x.DataHora descending, x.Id descending
        select new ExpedicaoResumo(
            x.Id, pd.Id, pd.Numero, c.Nome, u.Id, u.Nome, x.Transportadora, x.CodigoRastreio, x.DataHora);
}
