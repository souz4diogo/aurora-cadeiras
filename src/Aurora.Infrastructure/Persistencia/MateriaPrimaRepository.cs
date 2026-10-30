using Aurora.Application.Abstracoes;
using Aurora.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

public sealed class MateriaPrimaRepository(AuroraDbContext db) : IMateriaPrimaRepository
{
    public Task<MateriaPrima?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        db.MateriasPrimas.FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<bool> CodigoExisteAsync(string codigo, int? ignorarId, CancellationToken ct = default) =>
        db.MateriasPrimas.AnyAsync(m => m.Codigo == codigo && (ignorarId == null || m.Id != ignorarId), ct);

    public Task<bool> FornecedorExisteAsync(int fornecedorId, CancellationToken ct = default) =>
        db.Fornecedores.AnyAsync(f => f.Id == fornecedorId, ct);

    public async Task<(IReadOnlyList<MateriaPrima> Itens, int Total)> ListarAsync(
        string? busca, bool? ativo, int pagina, int tamanho, CancellationToken ct = default)
    {
        var consulta = db.MateriasPrimas.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(busca))
        {
            consulta = consulta.Where(m => m.Nome.Contains(busca) || m.Codigo.Contains(busca));
        }

        if (ativo is not null)
        {
            consulta = consulta.Where(m => m.Ativo == ativo);
        }

        var total = await consulta.CountAsync(ct);
        var itens = await consulta
            .OrderBy(m => m.Nome).ThenBy(m => m.Id)
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .ToListAsync(ct);
        return (itens, total);
    }

    public void Adicionar(MateriaPrima materiaPrima) => db.MateriasPrimas.Add(materiaPrima);
}
