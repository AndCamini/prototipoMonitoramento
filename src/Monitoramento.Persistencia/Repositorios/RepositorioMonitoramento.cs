using Microsoft.EntityFrameworkCore;
using Monitoramento.Dominio.Entidades;
using Monitoramento.Dominio.Enumeracoes;
using Monitoramento.Persistencia.Contratos;

namespace Monitoramento.Persistencia.Repositorios;

/// <summary>Gravação das verificações e dos alertas e recuperação do histórico.</summary>
public sealed class RepositorioMonitoramento(IDbContextFactory<MonitoramentoDbContext> fabrica) : IRepositorioMonitoramento
{
    public Task SalvarVerificacaoAsync(Verificacao verificacao, CancellationToken ct = default) =>
        SalvarVerificacoesAsync([verificacao], ct);

    public async Task SalvarVerificacoesAsync(IEnumerable<Verificacao> verificacoes, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        db.Verificacoes.AddRange(verificacoes);
        await db.SaveChangesAsync(ct);
    }

    public async Task SalvarAlertaAsync(Alerta alerta, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        db.Alertas.Add(alerta);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Alerta?> ObterAlertaAbertoAsync(Guid dispositivoId, Guid? servicoTcpId, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        return await ConsultarAberto(db, dispositivoId, servicoTcpId).AsNoTracking().FirstOrDefaultAsync(ct);
    }

    public async Task<Alerta?> EncerrarAlertaAbertoAsync(Guid dispositivoId, Guid? servicoTcpId, DateTime em, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var abertos = await ConsultarAberto(db, dispositivoId, servicoTcpId).ToListAsync(ct);
        foreach (var alerta in abertos)
            alerta.Encerrar(em);
        await db.SaveChangesAsync(ct);
        return abertos.FirstOrDefault();
    }

    public async Task EncerrarAlertasAbertosAsync(Guid dispositivoId, DateTime em, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var abertos = await db.Alertas
            .Where(a => a.DispositivoId == dispositivoId && a.EncerradoEm == null)
            .ToListAsync(ct);
        foreach (var alerta in abertos)
            alerta.Encerrar(em);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<Alerta>> ObterHistoricoAsync(Guid? dispositivoId, DateTime de, DateTime ate, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        return await db.Alertas.AsNoTracking()
            .Where(a => (dispositivoId == null || a.DispositivoId == dispositivoId) && a.IniciadoEm >= de && a.IniciadoEm <= ate)
            .OrderByDescending(a => a.IniciadoEm)
            .ToListAsync(ct);
    }

    private static IQueryable<Alerta> ConsultarAberto(MonitoramentoDbContext db, Guid dispositivoId, Guid? servicoTcpId) =>
        db.Alertas
            .Where(a => a.DispositivoId == dispositivoId
                        && a.ServicoTcpId == servicoTcpId
                        && a.Tipo == TipoEvento.Queda
                        && a.EncerradoEm == null)
            .OrderByDescending(a => a.IniciadoEm);
}
