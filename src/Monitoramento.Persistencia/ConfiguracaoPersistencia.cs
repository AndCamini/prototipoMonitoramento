using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Monitoramento.Persistencia.Contratos;
using Monitoramento.Persistencia.Repositorios;

namespace Monitoramento.Persistencia;

public static class ConfiguracaoPersistencia
{
    /// <summary>Registra o contexto do EF Core (Npgsql) e os três contratos da camada de persistência.</summary>
    public static IServiceCollection AddPersistencia(this IServiceCollection services, string connectionString)
    {
        services.AddDbContextFactory<MonitoramentoDbContext>(o => o.UseNpgsql(connectionString));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IRepositorioDispositivo, RepositorioDispositivo>();
        services.AddScoped<IRepositorioMonitoramento, RepositorioMonitoramento>();
        services.AddScoped<IConsultaPainel, ConsultaPainel>();
        return services;
    }

    /// <summary>
    /// Cria ou evolui o esquema por meio das migrações do EF Core (RNF07). Se o PostgreSQL ainda
    /// não estiver acessível (por exemplo, iniciando junto com a máquina), tenta novamente.
    /// </summary>
    public static async Task AplicarMigracoesAsync(this IServiceProvider provider, ILogger logger,
        int tentativas = 30, TimeSpan? espera = null, CancellationToken ct = default)
    {
        var intervalo = espera ?? TimeSpan.FromSeconds(5);
        var fabrica = provider.GetRequiredService<IDbContextFactory<MonitoramentoDbContext>>();

        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                await using var db = await fabrica.CreateDbContextAsync(ct);
                await db.Database.MigrateAsync(ct);
                logger.LogInformation("Esquema do banco de dados atualizado.");
                return;
            }
            catch (Exception ex) when (tentativa < tentativas && ex is not OperationCanceledException)
            {
                logger.LogWarning("Banco de dados indisponível ({Mensagem}). Nova tentativa em {Segundos}s ({Tentativa}/{Total}).",
                    ex.GetBaseException().Message, intervalo.TotalSeconds, tentativa, tentativas);
                await Task.Delay(intervalo, ct);
            }
        }
    }
}
