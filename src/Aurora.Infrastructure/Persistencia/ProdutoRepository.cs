using Aurora.Application.Abstracoes;
using Aurora.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

public sealed class ProdutoRepository(AuroraDbContext db) : IProdutoRepository
{
    public Task<Produto?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        db.Produtos.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> CodigoExisteAsync(string codigo, int? ignorarId, CancellationToken ct = default) =>
        db.Produtos.AnyAsync(p => p.Codigo == codigo && (ignorarId == null || p.Id != ignorarId), ct);

    public async Task<(IReadOnlyList<Produto> Itens, int Total)> ListarAsync(
        string? busca, bool? ativo, int pagina, int tamanho, CancellationToken ct = default)
    {
        var consulta = db.Produtos.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(busca))
        {
            consulta = consulta.Where(p => p.Nome.Contains(busca) || p.Codigo.Contains(busca));
        }

        if (ativo is not null)
        {
            consulta = consulta.Where(p => p.Ativo == ativo);
        }

        var total = await consulta.CountAsync(ct);
        var itens = await consulta
            .OrderBy(p => p.Nome).ThenBy(p => p.Id)
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .ToListAsync(ct);
        return (itens, total);
    }

    public void Adicionar(Produto produto) => db.Produtos.Add(produto);
}
