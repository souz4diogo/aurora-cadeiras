using Aurora.Application.Abstracoes;
using Aurora.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

public sealed class ClienteRepository(AuroraDbContext db) : IClienteRepository
{
    public Task<Cliente?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        db.Clientes.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> DocumentoExisteAsync(string cpfCnpj, int? ignorarId, CancellationToken ct = default) =>
        db.Clientes.AnyAsync(c => c.CpfCnpj == cpfCnpj && (ignorarId == null || c.Id != ignorarId), ct);

    public async Task<(IReadOnlyList<Cliente> Itens, int Total)> ListarAsync(
        string? busca, bool? ativo, int pagina, int tamanho, CancellationToken ct = default)
    {
        var consulta = db.Clientes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(busca))
        {
            consulta = consulta.Where(c => c.Nome.Contains(busca) || c.CpfCnpj.Contains(busca));
        }

        if (ativo is not null)
        {
            consulta = consulta.Where(c => c.Ativo == ativo);
        }

        var total = await consulta.CountAsync(ct);
        var itens = await consulta
            .OrderBy(c => c.Nome).ThenBy(c => c.Id)
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .ToListAsync(ct);
        return (itens, total);
    }

    public void Adicionar(Cliente cliente) => db.Clientes.Add(cliente);
}
