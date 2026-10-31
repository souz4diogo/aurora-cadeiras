using Aurora.Application.Abstracoes;
using Aurora.Application.Pedidos;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

public sealed class PedidoRepository(AuroraDbContext db) : IPedidoRepository
{
    /// <summary>Status em que o pedido já foi confirmado e ainda não saiu: é aqui que o prazo vencido conta como atraso.</summary>
    private static readonly StatusPedido[] StatusEmAndamento = [StatusPedido.Confirmado, StatusPedido.EmProducao, StatusPedido.Pronto];

    public Task<Pedido?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        db.Pedidos.Include(p => p.Itens).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<(IReadOnlyList<PedidoResumo> Itens, int Total)> ListarAsync(
        FiltroDePedidos filtro, int pagina, int tamanho, CancellationToken ct = default)
    {
        var pedidos = Filtrar(db.Pedidos.AsNoTracking(), filtro);

        var total = await pedidos.CountAsync(ct);
        var itens = await (
                from p in pedidos
                join c in db.Clientes on p.ClienteId equals c.Id
                orderby p.DataPedido descending, p.Id descending
                select new PedidoResumo(
                    p.Id, p.Numero, p.ClienteId, c.Nome, p.VendedorId, p.DataPedido, p.PrazoEntrega, p.Status, p.ValorTotal, p.Itens.Count))
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .ToListAsync(ct);
        return (itens, total);
    }

    public void Adicionar(Pedido pedido) => db.Pedidos.Add(pedido);

    public async Task ConfirmarAsync(int pedidoId, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.sp_ConfirmarPedido @PedidoId = {pedidoId}", ct);
        await AtualizarRastreadoAsync(pedidoId, ct);
    }

    public async Task CancelarAsync(int pedidoId, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.sp_CancelarPedido @PedidoId = {pedidoId}", ct);
        await AtualizarRastreadoAsync(pedidoId, ct);
    }

    /// <summary>A procedure mudou o status por fora do EF; atualiza a cópia rastreada.</summary>
    private async Task AtualizarRastreadoAsync(int pedidoId, CancellationToken ct)
    {
        var rastreado = db.ChangeTracker.Entries<Pedido>().FirstOrDefault(e => e.Entity.Id == pedidoId);
        if (rastreado is not null)
        {
            await rastreado.ReloadAsync(ct);
        }
    }

    private static IQueryable<Pedido> Filtrar(IQueryable<Pedido> consulta, FiltroDePedidos filtro)
    {
        if (filtro.VendedorId is int vendedorId)
        {
            consulta = consulta.Where(p => p.VendedorId == vendedorId);
        }

        if (filtro.Status is StatusPedido status)
        {
            consulta = consulta.Where(p => p.Status == status);
        }

        if (filtro.ClienteId is int clienteId)
        {
            consulta = consulta.Where(p => p.ClienteId == clienteId);
        }

        if (filtro.De is DateOnly de)
        {
            var inicio = de.ToDateTime(TimeOnly.MinValue);
            consulta = consulta.Where(p => p.DataPedido >= inicio);
        }

        if (filtro.Ate is DateOnly ate)
        {
            var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            consulta = consulta.Where(p => p.DataPedido < fim);
        }

        if (filtro.Atrasados is bool atrasados)
        {
            var hoje = filtro.Hoje;
            var emAndamento = StatusEmAndamento;
            consulta = atrasados
                ? consulta.Where(p => emAndamento.Contains(p.Status) && p.PrazoEntrega < hoje)
                : consulta.Where(p => !(emAndamento.Contains(p.Status) && p.PrazoEntrega < hoje));
        }

        return consulta;
    }
}
