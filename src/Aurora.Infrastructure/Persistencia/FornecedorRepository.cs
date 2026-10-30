using Aurora.Application.Abstracoes;
using Aurora.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

public sealed class FornecedorRepository(AuroraDbContext db) : IFornecedorRepository
{
    public Task<Fornecedor?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        db.Fornecedores.FirstOrDefaultAsync(f => f.Id == id, ct);

    public Task<bool> CnpjExisteAsync(string cnpj, int? ignorarId, CancellationToken ct = default) =>
        db.Fornecedores.AnyAsync(f => f.Cnpj == cnpj && (ignorarId == null || f.Id != ignorarId), ct);

    public async Task<(IReadOnlyList<Fornecedor> Itens, int Total)> ListarAsync(
        string? busca, bool? ativo, bool? comunidadeTradicional, int pagina, int tamanho, CancellationToken ct = default)
    {
        var consulta = db.Fornecedores.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(busca))
        {
            consulta = consulta.Where(f => f.RazaoSocial.Contains(busca) || f.Cnpj.Contains(busca));
        }

        if (ativo is not null)
        {
            consulta = consulta.Where(f => f.Ativo == ativo);
        }

        if (comunidadeTradicional is not null)
        {
            consulta = consulta.Where(f => f.ComunidadeTradicional == comunidadeTradicional);
        }

        var total = await consulta.CountAsync(ct);
        var itens = await consulta
            .OrderBy(f => f.RazaoSocial).ThenBy(f => f.Id)
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .ToListAsync(ct);
        return (itens, total);
    }

    public void Adicionar(Fornecedor fornecedor) => db.Fornecedores.Add(fornecedor);
}
