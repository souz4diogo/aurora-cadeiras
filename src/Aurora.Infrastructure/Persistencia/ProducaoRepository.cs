using System.Linq.Expressions;
using Aurora.Application.Abstracoes;
using Aurora.Application.Producao;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

public sealed class ProducaoRepository(AuroraDbContext db) : IProducaoRepository
{
    private static readonly StatusOrdemProducao[] StatusEmAberto = [StatusOrdemProducao.Planejada, StatusOrdemProducao.EmAndamento];

    public async Task<(IReadOnlyList<OrdemProducaoResumo> Itens, int Total)> ListarOrdensAsync(
        FiltroDeOrdens filtro, int pagina, int tamanho, CancellationToken ct = default)
    {
        var ordens = Filtrar(db.OrdensProducao.AsNoTracking(), filtro);
        var total = await ordens.CountAsync(ct);

        var ordenadas = filtro.SomenteAbertas
            ? ordens.OrderBy(o => o.PrevisaoConclusao == null).ThenBy(o => o.PrevisaoConclusao).ThenBy(o => o.Id)
            : ordens.OrderByDescending(o => o.CriadaEm).ThenByDescending(o => o.Id);
        var pagina1 = ordenadas.Skip((pagina - 1) * tamanho).Take(tamanho);

        return (await MontarResumosAsync(pagina1, ct), total);
    }

    public async Task<OrdemProducaoDetalhe?> ObterOrdemAsync(int id, int? vendedorId, CancellationToken ct = default)
    {
        var ordens = db.OrdensProducao.AsNoTracking().Where(o => o.Id == id);
        if (vendedorId is int vendedor)
        {
            ordens = ordens.Where(DoVendedor(vendedor));
        }

        var resumo = (await MontarResumosAsync(ordens, ct)).SingleOrDefault();
        if (resumo is null)
        {
            return null;
        }

        var cliente = await (
                from o in ordens
                join pi in db.Set<PedidoItem>() on o.PedidoItemId equals pi.Id
                join pd in db.Pedidos on pi.PedidoId equals pd.Id
                join c in db.Clientes on pd.ClienteId equals c.Id
                select new { o.CodigoQr, ClienteNome = c.Nome })
            .SingleAsync(ct);

        var etapas = await ConsultarEtapas(e => e.OrdemProducaoId == id).ToListAsync(ct);

        var apontamentos = await (
                from a in db.Apontamentos.AsNoTracking()
                join e in db.Etapas on a.OrdemProducaoEtapaId equals e.Id
                join u in db.Usuarios on a.UsuarioId equals u.Id
                where e.OrdemProducaoId == id
                orderby a.DataHoraServidor, a.Id
                select new ApontamentoDto(
                    a.Id, e.Id, a.Tipo.ToString(), u.Id, u.Nome, a.DataHoraCliente, a.DataHoraServidor))
            .ToListAsync(ct);

        var perdas = await (
                from p in db.Perdas.AsNoTracking()
                join e in db.Etapas on p.OrdemProducaoEtapaId equals e.Id
                join es in db.Estacoes on e.EstacaoId equals es.Id
                join u in db.Usuarios on p.UsuarioId equals u.Id
                where e.OrdemProducaoId == id
                orderby p.CriadoEm, p.Id
                select new PerdaDto(
                    p.Id, e.Id, es.Nome, p.Tipo.ToString(), p.Quantidade, p.Motivo, u.Id, u.Nome, p.CriadoEm))
            .ToListAsync(ct);

        return new OrdemProducaoDetalhe(resumo, cliente.CodigoQr, cliente.ClienteNome, etapas, apontamentos, perdas);
    }

    public Task<EtapaResumo?> ObterEtapaAsync(int etapaId, CancellationToken ct = default) =>
        ConsultarEtapas(e => e.Id == etapaId).SingleOrDefaultAsync(ct);

    public async Task IniciarEtapaAsync(int etapaId, Guid clientRequestId, DateTime dataHoraClienteUtc, CancellationToken ct = default) =>
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC dbo.sp_IniciarEtapa @OrdemProducaoEtapaId = {etapaId}, @ClientRequestId = {clientRequestId}, @DataHoraCliente = {dataHoraClienteUtc}",
            ct);

    public async Task FinalizarEtapaAsync(int etapaId, Guid clientRequestId, DateTime dataHoraClienteUtc, CancellationToken ct = default) =>
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC dbo.sp_FinalizarEtapa @OrdemProducaoEtapaId = {etapaId}, @ClientRequestId = {clientRequestId}, @DataHoraCliente = {dataHoraClienteUtc}",
            ct);

    public void AdicionarPerda(RegistroPerda perda) => db.Perdas.Add(perda);

    /// <summary>Etapas que atendem ao filtro, com a OP, a estação e (se houver) o operador, por sequência.</summary>
    private IQueryable<EtapaResumo> ConsultarEtapas(Expression<Func<OrdemProducaoEtapa, bool>> onde) =>
        from e in db.Etapas.AsNoTracking().Where(onde)
        join o in db.OrdensProducao on e.OrdemProducaoId equals o.Id
        join es in db.Estacoes on e.EstacaoId equals es.Id
        join u in db.Usuarios on e.OperadorId equals (int?)u.Id into operadores
        from u in operadores.DefaultIfEmpty()
        orderby e.Sequencia
        select new EtapaResumo(
            e.Id,
            e.OrdemProducaoId,
            o.Numero,
            o.Status,
            o.Quantidade,
            e.EstacaoId,
            es.Nome,
            e.Sequencia,
            e.Status,
            e.IniciadaEm,
            e.ConcluidaEm,
            e.OperadorId,
            u != null ? u.Nome : null);

    /// <summary>
    /// Projeta a página de OPs em resumos: uma consulta para os dados da OP, do produto e do pedido e outra para as
    /// etapas de todas elas, de onde saem a etapa atual (a primeira não concluída) e o progresso.
    /// </summary>
    private async Task<List<OrdemProducaoResumo>> MontarResumosAsync(IQueryable<OrdemProducao> ordens, CancellationToken ct)
    {
        var linhas = await (
                from o in ordens
                join pi in db.Set<PedidoItem>() on o.PedidoItemId equals pi.Id
                join pd in db.Pedidos on pi.PedidoId equals pd.Id
                join p in db.Produtos on o.ProdutoId equals p.Id
                select new
                {
                    Ordem = o,
                    PedidoId = pd.Id,
                    PedidoNumero = pd.Numero,
                    ProdutoCodigo = p.Codigo,
                    ProdutoNome = p.Nome,
                })
            .ToListAsync(ct);

        var ids = linhas.Select(l => l.Ordem.Id).ToList();
        var etapas = (await (
                    from e in db.Etapas.AsNoTracking()
                    join es in db.Estacoes on e.EstacaoId equals es.Id
                    where ids.Contains(e.OrdemProducaoId)
                    select new { Etapa = e, EstacaoNome = es.Nome })
                .ToListAsync(ct))
            .ToLookup(x => x.Etapa.OrdemProducaoId);

        return linhas.Select(l =>
        {
            var dela = etapas[l.Ordem.Id].OrderBy(x => x.Etapa.Sequencia).ToList();
            var atual = dela.FirstOrDefault(x => x.Etapa.Status != StatusEtapa.Concluida);
            return new OrdemProducaoResumo(
                l.Ordem.Id,
                l.Ordem.Numero,
                l.PedidoId,
                l.PedidoNumero,
                l.Ordem.ProdutoId,
                l.ProdutoCodigo,
                l.ProdutoNome,
                l.Ordem.Quantidade,
                l.Ordem.Status,
                l.Ordem.PrevisaoConclusao,
                l.Ordem.CriadaEm,
                l.Ordem.ConcluidaEm,
                dela.Count,
                dela.Count(x => x.Etapa.Status == StatusEtapa.Concluida),
                atual is null
                    ? null
                    : new EtapaAtualResumo(
                        atual.Etapa.Id, atual.Etapa.EstacaoId, atual.EstacaoNome, atual.Etapa.Sequencia, atual.Etapa.Status, atual.Etapa.IniciadaEm));
        }).ToList();
    }

    private IQueryable<OrdemProducao> Filtrar(IQueryable<OrdemProducao> consulta, FiltroDeOrdens filtro)
    {
        if (filtro.VendedorId is int vendedorId)
        {
            consulta = consulta.Where(DoVendedor(vendedorId));
        }

        if (filtro.PedidoId is int pedidoId)
        {
            consulta = consulta.Where(o => db.Set<PedidoItem>().Any(pi => pi.Id == o.PedidoItemId && pi.PedidoId == pedidoId));
        }

        if (filtro.Status is StatusOrdemProducao status)
        {
            consulta = consulta.Where(o => o.Status == status);
        }

        if (filtro.SomenteAbertas)
        {
            var abertas = StatusEmAberto;
            consulta = consulta.Where(o => abertas.Contains(o.Status));
        }

        if (filtro.EstacaoId is byte estacaoId)
        {
            // A estação atual é a da primeira etapa ainda não concluída
            consulta = consulta.Where(o => o.Etapas.Any(e =>
                e.EstacaoId == estacaoId
                && e.Status != StatusEtapa.Concluida
                && !o.Etapas.Any(a => a.Sequencia < e.Sequencia && a.Status != StatusEtapa.Concluida)));
        }

        if (filtro.Atrasadas is bool atrasadas)
        {
            var hoje = filtro.Hoje;
            var abertas = StatusEmAberto;
            consulta = atrasadas
                ? consulta.Where(o => abertas.Contains(o.Status) && o.PrevisaoConclusao < hoje)
                : consulta.Where(o => !(abertas.Contains(o.Status) && o.PrevisaoConclusao < hoje));
        }

        return consulta;
    }

    /// <summary>Expressão (e não método) porque o EF a traduz para SQL: OP de item de pedido do vendedor.</summary>
    private Expression<Func<OrdemProducao, bool>> DoVendedor(int vendedorId) =>
        ordem => db.Set<PedidoItem>().Any(pi =>
            pi.Id == ordem.PedidoItemId && db.Pedidos.Any(pd => pd.Id == pi.PedidoId && pd.VendedorId == vendedorId));
}
