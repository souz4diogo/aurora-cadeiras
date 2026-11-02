using Aurora.Application.Abstracoes;
using Aurora.Application.Compras;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

public sealed class PedidoCompraRepository(AuroraDbContext db) : IPedidoCompraRepository
{
    private static readonly StatusPedidoCompra[] EmRecebimento = [StatusPedidoCompra.Emitido, StatusPedidoCompra.ParcialmenteRecebido];

    public Task<PedidoCompra?> ObterAsync(int id, CancellationToken ct = default) =>
        db.PedidosCompra.AsNoTracking().Include(p => p.Itens).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<(IReadOnlyList<PedidoCompraResumo> Itens, int Total)> ListarAsync(
        FiltroDePedidosCompra filtro, int pagina, int tamanho, CancellationToken ct = default)
    {
        var pedidos = Filtrar(db.PedidosCompra.AsNoTracking(), filtro);

        var total = await pedidos.CountAsync(ct);
        var itens = await (
                from p in pedidos
                join f in db.Fornecedores on p.FornecedorId equals f.Id
                orderby p.CriadoEm descending, p.Id descending
                select new PedidoCompraResumo(
                    p.Id,
                    p.Numero,
                    p.FornecedorId,
                    f.RazaoSocial,
                    p.Status,
                    p.DataPrevista,
                    p.CriadoEm,
                    p.Itens.Sum(i => i.Quantidade * i.CustoUnitario),
                    p.Itens.Count))
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .ToListAsync(ct);
        return (itens, total);
    }

    public void Adicionar(PedidoCompra pedido) => db.PedidosCompra.Add(pedido);

    public async Task<bool> TentarEmitirAsync(int id, CancellationToken ct = default) =>
        await db.PedidosCompra
            .Where(p => p.Id == id && p.Status == StatusPedidoCompra.Solicitado)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, StatusPedidoCompra.Emitido), ct) == 1;

    public async Task<bool> TentarCancelarAsync(int id, CancellationToken ct = default) =>
        await db.PedidosCompra
            .Where(p => p.Id == id
                && (p.Status == StatusPedidoCompra.Solicitado || p.Status == StatusPedidoCompra.Emitido)
                && !p.Itens.Any(i => i.QuantidadeRecebida > 0))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, StatusPedidoCompra.Cancelado), ct) == 1;

    public async Task<int?> ObterPedidoDoItemAsync(int itemId, CancellationToken ct = default) =>
        await db.Set<PedidoCompraItem>().AsNoTracking()
            .Where(i => i.Id == itemId)
            .Select(i => (int?)i.PedidoCompraId)
            .FirstOrDefaultAsync(ct);

    public Task ReceberItemAsync(int itemId, decimal quantidade, CancellationToken ct = default) =>
        db.ExecutarProcedureAsync(
            "EXEC dbo.sp_ReceberItemCompra @PedidoCompraItemId = @item, @Quantidade = @quantidade",
            [Parametro.Inteiro("@item", itemId), Parametro.Numerica("@quantidade", quantidade, 12, 3)],
            ct);

    private static IQueryable<PedidoCompra> Filtrar(IQueryable<PedidoCompra> consulta, FiltroDePedidosCompra filtro)
    {
        if (filtro.Status is StatusPedidoCompra status)
        {
            consulta = consulta.Where(p => p.Status == status);
        }

        if (filtro.FornecedorId is int fornecedorId)
        {
            consulta = consulta.Where(p => p.FornecedorId == fornecedorId);
        }

        if (filtro.De is DateOnly de)
        {
            var inicio = de.ToDateTime(TimeOnly.MinValue);
            consulta = consulta.Where(p => p.CriadoEm >= inicio);
        }

        if (filtro.Ate is DateOnly ate)
        {
            var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            consulta = consulta.Where(p => p.CriadoEm < fim);
        }

        if (filtro.Atrasados is bool atrasados)
        {
            var hoje = filtro.Hoje;
            var emRecebimento = EmRecebimento;
            consulta = atrasados
                ? consulta.Where(p => emRecebimento.Contains(p.Status) && p.DataPrevista < hoje)
                : consulta.Where(p => !(emRecebimento.Contains(p.Status) && p.DataPrevista < hoje));
        }

        return consulta;
    }
}
