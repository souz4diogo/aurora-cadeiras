using Aurora.Application.Abstracoes;
using Aurora.Application.Auditoria;
using Microsoft.EntityFrameworkCore;

namespace Aurora.Infrastructure.Persistencia;

public sealed class AuditoriaRepository(AuroraDbContext db) : IAuditoriaRepository
{
    public async Task<(IReadOnlyList<AuditoriaResumo> Itens, int Total)> ListarAsync(
        FiltroDeAuditoria filtro, int pagina, int tamanho, CancellationToken ct = default)
    {
        var linhas = db.Auditorias.AsNoTracking();

        if (filtro.Tabela is not null)
        {
            linhas = linhas.Where(x => x.Tabela == filtro.Tabela);
        }

        if (filtro.Registro is not null)
        {
            linhas = linhas.Where(x => x.Registro == filtro.Registro);
        }

        if (filtro.UsuarioId is int usuarioId)
        {
            linhas = linhas.Where(x => x.UsuarioId == usuarioId);
        }

        if (filtro.De is DateOnly de)
        {
            var inicio = de.ToDateTime(TimeOnly.MinValue);
            linhas = linhas.Where(x => x.DataHora >= inicio);
        }

        if (filtro.Ate is DateOnly ate)
        {
            var fim = ate.AddDays(1).ToDateTime(TimeOnly.MinValue);
            linhas = linhas.Where(x => x.DataHora < fim);
        }

        var total = await linhas.CountAsync(ct);
        var itens = await (
                from x in linhas
                join u in db.Usuarios on x.UsuarioId equals u.Id into usuarios
                from u in usuarios.DefaultIfEmpty()
                orderby x.DataHora descending, x.Id descending
                select new AuditoriaResumo(
                    x.Id, x.Tabela, x.Registro, x.Acao, x.UsuarioId, u == null ? null : u.Nome, x.DataHora, x.Antes, x.Depois))
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .ToListAsync(ct);
        return (itens, total);
    }
}
