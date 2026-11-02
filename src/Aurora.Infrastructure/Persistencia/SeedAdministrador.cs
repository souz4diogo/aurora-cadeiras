using Aurora.Application.Abstracoes;
using Aurora.Application.Autenticacao;
using Aurora.Domain.Entidades;
using Aurora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aurora.Infrastructure.Persistencia;

public static class SeedAdministrador
{
    /// <summary>
    /// Cria o administrador inicial se não houver nenhum usuário e a senha vier da configuração
    /// (Seed:AdminPassword ou a variável SEED_ADMIN_PASSWORD). A senha nunca fica no repositório.
    /// </summary>
    public static async Task SemearAdministradorAsync(this IServiceProvider servicos, CancellationToken ct = default)
    {
        await using var escopo = servicos.CreateAsyncScope();
        var configuracao = escopo.ServiceProvider.GetRequiredService<IConfiguration>();
        var senha = configuracao["Seed:AdminPassword"] ?? Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(senha))
        {
            return;
        }

        var db = escopo.ServiceProvider.GetRequiredService<AuroraDbContext>();
        if (await db.Usuarios.AnyAsync(ct))
        {
            return;
        }

        // Falha fechada: preferimos não ter administrador a ter um com senha fraca (docs/06 §2)
        if (!PoliticaDeSenha.Atende(senha))
        {
            throw new InvalidOperationException($"SEED_ADMIN_PASSWORD não atende à política de senha. {PoliticaDeSenha.Descricao}");
        }

        var hasher = escopo.ServiceProvider.GetRequiredService<ISenhaHasher>();
        var email = (configuracao["Seed:AdminEmail"] ?? "admin@aurora.local").Trim().ToLowerInvariant();
        db.Usuarios.Add(new Usuario("Administrador", email, hasher.Hash(senha), PerfilUsuario.Administrador, null));
        await db.SaveChangesAsync(ct);
    }
}
