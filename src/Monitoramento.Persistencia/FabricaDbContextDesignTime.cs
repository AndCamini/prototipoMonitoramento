using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Monitoramento.Persistencia;

/// <summary>Usada apenas pelas ferramentas do EF Core (dotnet ef migrations add ...).</summary>
internal sealed class FabricaDbContextDesignTime : IDesignTimeDbContextFactory<MonitoramentoDbContext>
{
    public MonitoramentoDbContext CreateDbContext(string[] args)
    {
        var conexao = Environment.GetEnvironmentVariable("MONITORAMENTO_CONEXAO")
                      ?? "Host=localhost;Port=5432;Database=monitoramento;Username=monitoramento;Password=monitoramento";
        var options = new DbContextOptionsBuilder<MonitoramentoDbContext>().UseNpgsql(conexao).Options;
        return new MonitoramentoDbContext(options);
    }
}
