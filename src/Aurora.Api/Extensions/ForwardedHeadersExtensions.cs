using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Aurora.Api.Extensions;

public static class ForwardedHeadersExtensions
{
    private const string Secao = "ForwardedHeaders";

    /// <summary>
    /// Atrás de um proxy ou ingress (Container Apps, Front Door), o IP de quem conecta é o do proxy; sem isto o rate limit
    /// do login trataria todos os usuários como um só. Desligado por padrão: aceitar X-Forwarded-For de qualquer origem
    /// permitiria forjar o IP e burlar o limite. Para ligar, defina <c>ForwardedHeaders:Habilitado=true</c> e, se possível,
    /// <c>ProxiesConfiaveis</c> (IPs) ou <c>RedesConfiaveis</c> (CIDR). Sem lista, confia no proxy imediato, o que só é
    /// seguro quando a API é alcançável exclusivamente pelo ingress.
    /// </summary>
    public static IServiceCollection AddAuroraForwardedHeaders(this IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersOptions>().Configure<IConfiguration>((opcoes, configuracao) =>
        {
            var secao = configuracao.GetSection(Secao);
            if (!secao.GetValue<bool>("Habilitado"))
            {
                return;
            }

            opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            opcoes.ForwardLimit = 1; // só o último salto, que foi o proxy confiável quem acrescentou

            // Os padrões confiam apenas em loopback; substituímos pela configuração do ambiente
            opcoes.KnownProxies.Clear();
            opcoes.KnownIPNetworks.Clear();
            foreach (var proxy in secao.GetSection("ProxiesConfiaveis").Get<string[]>() ?? [])
            {
                opcoes.KnownProxies.Add(IPAddress.Parse(proxy));
            }

            foreach (var rede in secao.GetSection("RedesConfiaveis").Get<string[]>() ?? [])
            {
                opcoes.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(rede));
            }
        });
        return services;
    }
}
