using Aurora.Application.Abstracoes;
using Aurora.Infrastructure.Persistencia;
using Aurora.Infrastructure.Seguranca;
using Aurora.Infrastructure.Tempo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Aurora.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAuroraInfrastructure(this IServiceCollection services, IConfiguration configuracao)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuracao.GetSection(JwtOptions.Secao))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // A connection string é lida só quando o DbContext é criado, para respeitar toda a configuração já montada
        services.AddScoped<SessionContextInterceptor>();
        services.AddDbContext<AuroraDbContext>((sp, opcoes) =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("Aurora")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings:Aurora não configurada. Defina a variável de ambiente ConnectionStrings__Aurora.");
            opcoes.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3))
                .AddInterceptors(sp.GetRequiredService<SessionContextInterceptor>());
        });

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        services.AddSingleton<IRelogio, RelogioDoSistema>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<ISenhaHasher, SenhaHasher>();
        services.AddSingleton<IGeradorDeSenha, GeradorDeSenha>();
        return services;
    }
}

