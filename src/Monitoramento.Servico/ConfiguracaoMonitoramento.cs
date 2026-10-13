using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Monitoramento.Dominio.Abstracoes;
using Monitoramento.Servico.Verificadores;

namespace Monitoramento.Servico;

public static class ConfiguracaoMonitoramento
{
    /// <summary>Registra o worker, os verificadores ICMP/TCP, o avaliador e o notificador de estado.</summary>
    public static IServiceCollection AddServicoMonitoramento(this IServiceCollection services, IConfiguration configuracao)
    {
        services.Configure<MonitoramentoOptions>(configuracao.GetSection(MonitoramentoOptions.Secao));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<INotificadorDeEstado, NotificadorDeEstado>();
        services.AddSingleton<IVerificador, VerificadorIcmp>();
        services.AddSingleton<IVerificador, VerificadorTcp>();
        services.AddSingleton<AvaliadorDeEstado>();
        services.AddHostedService<MonitoramentoWorker>();
        return services;
    }
}
