using Aurora.Application.Abstracoes;
using Aurora.Application.Estoque;
using Aurora.Domain.Entidades;
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
