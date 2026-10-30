using Aurora.Application.Abstracoes;
using Aurora.Domain.Entidades;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

public sealed class UsuarioRepository(AuroraDbContext db) : IUsuarioRepository
{
    public Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken ct = default) =>
        db.Usuarios.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<Usuario?> ObterPorIdAsync(int id, CancellationToken ct = default) =>
        db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> EmailExisteAsync(string email, int? ignorarId, CancellationToken ct = default) =>
        db.Usuarios.AnyAsync(u => u.Email == email && (ignorarId == null || u.Id != ignorarId), ct);

    public Task<bool> ClienteExisteAsync(int clienteId, CancellationToken ct = default) =>
        db.Database.SqlQuery<int>($"SELECT ClienteId AS Value FROM dbo.Cliente WHERE ClienteId = {clienteId}").AnyAsync(ct);

    public async Task<(IReadOnlyList<Usuario> Itens, int Total)> ListarAsync(
        string? busca, int pagina, int tamanho, CancellationToken ct = default)
    {
        var consulta = db.Usuarios.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(busca))
        {
            consulta = consulta.Where(u => u.Nome.Contains(busca) || u.Email.Contains(busca));
        }

        var total = await consulta.CountAsync(ct);
        var itens = await consulta
            .OrderBy(u => u.Nome).ThenBy(u => u.Id)
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .ToListAsync(ct);
        return (itens, total);
    }

    public void Adicionar(Usuario usuario) => db.Usuarios.Add(usuario);
}
