using Microsoft.EntityFrameworkCore;
using Monitoramento.Dominio.Entidades;

namespace Monitoramento.Persistencia;

/// <summary>Contexto do Entity Framework Core que mapeia as entidades para o PostgreSQL.</summary>
public class MonitoramentoDbContext(DbContextOptions<MonitoramentoDbContext> options) : DbContext(options)
{
    public DbSet<Dispositivo> Dispositivos => Set<Dispositivo>();
    public DbSet<ServicoTcp> Servicos => Set<ServicoTcp>();
    public DbSet<Verificacao> Verificacoes => Set<Verificacao>();
    public DbSet<Alerta> Alertas => Set<Alerta>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MonitoramentoDbContext).Assembly);
}
