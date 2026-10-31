using Aurora.Application.Abstracoes;
using Aurora.Application.Estoque;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

public sealed class EstoqueRepository(AuroraDbContext db) : IEstoqueRepository
{
    public async Task<(IReadOnlyList<MovimentoEstoqueResumo> Itens, int Total)> ListarMovimentosAsync(
        FiltroDeMovimentos filtro, int pagina, int tamanho, CancellationToken ct = default)
    {
        var movimentos = Filtrar(db.Movimentos.AsNoTracking(), filtro);

        var total = await movimentos.CountAsync(ct);
        var itens = await (
                from m in movimentos
                join mp in db.MateriasPrimas on m.MateriaPrimaId equals (int?)mp.Id into materias
                from mp in materias.DefaultIfEmpty()
                join p in db.Produtos on m.ProdutoId equals (int?)p.Id into produtos
                from p in produtos.DefaultIfEmpty()
                join u in db.Usuarios on m.UsuarioId equals (int?)u.Id into usuarios
                from u in usuarios.DefaultIfEmpty()
                orderby m.DataHora descending, m.Id descending
                select new MovimentoEstoqueResumo(
                    m.Id,
                    m.DataHora,
                    m.Tipo,
                    m.Origem,
                    m.OrigemId,
                    m.MateriaPrimaId,
                    m.ProdutoId,
                    mp != null ? mp.Codigo : p!.Codigo,
                    mp != null ? mp.Nome : p!.Nome,
                    m.Quantidade,
                    m.UsuarioId,
                    u != null ? u.Nome : null,
                    m.Motivo))
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .ToListAsync(ct);
        return (itens, total);
    }

    public async Task AjustarMateriaPrimaAsync(
        int materiaPrimaId, decimal novaQuantidade, string motivo, CancellationToken ct = default) =>
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"EXEC dbo.sp_AjustarEstoque @MateriaPrimaId = {materiaPrimaId}, @NovaQuantidade = {novaQuantidade}, @Motivo = {motivo}", ct);

    public async Task<(IReadOnlyList<SaldoDeMateriaPrimaDto> Itens, int Total)> ListarSaldosDeMateriaPrimaAsync(
        string? busca, bool? critico, int pagina, int tamanho, CancellationToken ct = default)
    {
        var materias = db.MateriasPrimas.AsNoTracking().Where(m => m.Ativo);
        if (busca is not null)
        {
            materias = materias.Where(m => m.Nome.Contains(busca) || m.Codigo.Contains(busca));
        }

        if (critico is true)
        {
            materias = materias.Where(m => m.EstoqueAtual <= m.EstoqueMinimo);
        }
        else if (critico is false)
        {
            materias = materias.Where(m => m.EstoqueAtual > m.EstoqueMinimo);
        }

        var total = await materias.CountAsync(ct);
        var itens = await (
                from m in materias
                join f in db.Fornecedores on m.FornecedorPreferencialId equals (int?)f.Id into fornecedores
                from f in fornecedores.DefaultIfEmpty()
                orderby m.Nome, m.Id
                select new { Materia = m, Fornecedor = f == null ? null : f.RazaoSocial })
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .ToListAsync(ct);

        return (itens.Select(x => new SaldoDeMateriaPrimaDto(
                x.Materia.Id,
                x.Materia.Codigo,
                x.Materia.Nome,
                x.Materia.Unidade.ParaCodigo(),
                x.Materia.EstoqueAtual,
                x.Materia.EstoqueMinimo,
                x.Materia.EstoqueAtual <= x.Materia.EstoqueMinimo,
                x.Materia.FornecedorPreferencialId,
                x.Fornecedor)).ToList(),
            total);
    }

    public async Task<(IReadOnlyList<SaldoDeProdutoDto> Itens, int Total)> ListarSaldosDeProdutoAsync(
        string? busca, int pagina, int tamanho, CancellationToken ct = default)
    {
        var produtos = db.Produtos.AsNoTracking().Where(p => p.Ativo);
        if (busca is not null)
        {
            produtos = produtos.Where(p => p.Nome.Contains(busca) || p.Codigo.Contains(busca));
        }

        var total = await produtos.CountAsync(ct);
        var itens = await (
                from p in produtos
                join e in db.EstoquesDeProduto on p.Id equals e.ProdutoId into estoques
                from e in estoques.DefaultIfEmpty()
                orderby p.Codigo
                select new SaldoDeProdutoDto(
                    p.Id,
                    p.Codigo,
                    p.Nome,
                    e == null ? 0 : e.Quantidade,
                    e == null ? 0 : e.Reservado,
                    e == null ? 0 : e.Quantidade - e.Reservado))
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .ToListAsync(ct);
        return (itens, total);
    }

    public async Task<(IReadOnlyList<AlertaDeEstoqueDto> Itens, int Total)> ListarAlertasAbertosAsync(
        int pagina, int tamanho, CancellationToken ct = default)
    {
        var abertos = db.Alertas.AsNoTracking().Where(a => a.LidoEm == null);
        var total = await abertos.CountAsync(ct);
        return (await LerAlertasAsync(abertos, (pagina - 1) * tamanho, tamanho, ct), total);
    }

    public Task<AlertaEstoque?> ObterAlertaAsync(int id, CancellationToken ct = default) =>
        db.Alertas.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<AlertaDeEstoqueDto?> ObterAlertaDetalhadoAsync(int id, CancellationToken ct = default) =>
        (await LerAlertasAsync(db.Alertas.AsNoTracking().Where(a => a.Id == id), 0, 1, ct)).SingleOrDefault();

    /// <summary>Alertas com os dados do material, do mais antigo ao mais novo; a unidade é convertida em memória.</summary>
    private async Task<List<AlertaDeEstoqueDto>> LerAlertasAsync(
        IQueryable<AlertaEstoque> alertas, int pular, int pegar, CancellationToken ct)
    {
        var linhas = await (
                from a in alertas
                join m in db.MateriasPrimas on a.MateriaPrimaId equals m.Id
                orderby a.CriadoEm, a.Id
                select new { Alerta = a, Materia = m })
            .Skip(pular).Take(pegar)
            .ToListAsync(ct);

        return linhas.Select(x => new AlertaDeEstoqueDto(
            x.Alerta.Id,
            x.Materia.Id,
            x.Materia.Codigo,
            x.Materia.Nome,
            x.Materia.Unidade.ParaCodigo(),
            x.Materia.EstoqueAtual,
            x.Materia.EstoqueMinimo,
            x.Alerta.CriadoEm,
            x.Alerta.LidoEm,
            x.Alerta.LidoPorId)).ToList();
    }

    private static IQueryable<MovimentoEstoque> Filtrar(IQueryable<MovimentoEstoque> consulta, FiltroDeMovimentos filtro)
    {
        if (filtro.MateriaPrimaId is int materiaPrimaId)
        {
            consulta = consulta.Where(m => m.MateriaPrimaId == materiaPrimaId);
        }

        if (filtro.ProdutoId is int produtoId)
        {
            consulta = consulta.Where(m => m.ProdutoId == produtoId);
        }

        if (filtro.Tipo is { } tipo)
        {
            consulta = consulta.Where(m => m.Tipo == tipo);
        }

        if (filtro.Origem is { } origem)
        {
            consulta = consulta.Where(m => m.Origem == origem);
        }

        if (filtro.De is DateOnly de)
        {
            var inicio = de.ToDateTime(TimeOnly.MinValue);
            consulta = consulta.Where(m => m.DataHora >= inicio);
        }

        if (filtro.Ate is DateOnly ate)
        {
            var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            consulta = consulta.Where(m => m.DataHora < fim);
        }

        return consulta;
    }
}
