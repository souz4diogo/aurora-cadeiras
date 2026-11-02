using Microsoft.AspNetCore.DataProtection;

namespace Aurora.Web.Extensions;

public static class ProtecaoDeDadosExtensions
{
    /// <summary>
    /// Com <c>DataProtection:CaminhoDasChaves</c> definido, as chaves que protegem o cookie de sessão e o token
    /// anti-CSRF ficam nessa pasta (na nuvem, um compartilhamento de arquivos montado no Container App). Sem isso cada
    /// instância gera as próprias chaves: com 2 ou mais instâncias, ou a cada nova revisão, o cookie emitido por uma
    /// não vale na outra e o usuário é deslogado. Sem o caminho (desenvolvimento, testes, compose local) o
    /// comportamento padrão do ASP.NET Core continua.
    /// </summary>
    public static IServiceCollection AddAuroraProtecaoDeDados(this IServiceCollection services, IConfiguration configuracao)
    {
        var caminho = configuracao["DataProtection:CaminhoDasChaves"];
        if (!string.IsNullOrWhiteSpace(caminho))
        {
            services.AddDataProtection()
                .SetApplicationName("aurora-web")
                .PersistKeysToFileSystem(new DirectoryInfo(caminho));
        }

        return services;
    }
}
