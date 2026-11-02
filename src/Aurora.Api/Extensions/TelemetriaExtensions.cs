using Azure.Monitor.OpenTelemetry.AspNetCore;

namespace Aurora.Api.Extensions;

public static class TelemetriaExtensions
{
    /// <summary>
    /// Envia métricas, rastros e logs ao Application Insights (docs/09 §4) quando existe a connection string
    /// (<c>APPLICATIONINSIGHTS_CONNECTION_STRING</c>, definida no Container App). Sem ela, nada é registrado: o
    /// desenvolvimento local e os testes não dependem de nuvem. Não coleta o texto dos comandos SQL, e a
    /// instrumentação do ASP.NET Core mascara o valor dos parâmetros de consulta; nenhum dado pessoal ou token é
    /// acrescentado (os logs da aplicação já não os trazem).
    /// </summary>
    public static IServiceCollection AddAuroraTelemetria(this IServiceCollection services, IConfiguration configuracao)
    {
        if (!string.IsNullOrWhiteSpace(configuracao["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            services.AddOpenTelemetry().UseAzureMonitor();
        }

        return services;
    }
}
