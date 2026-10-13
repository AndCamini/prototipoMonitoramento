using Microsoft.EntityFrameworkCore;
using Monitoramento.Dominio.Entidades;
using Monitoramento.Persistencia.Contratos;

namespace Monitoramento.Persistencia.Repositorios;

/// <summary>
/// Cadastro e leitura dos dispositivos. Cada operação usa um contexto próprio, o que permite
/// que o serviço de monitoramento verifique vários dispositivos em paralelo (RNF04) e que a
/// interface Blazor não mantenha um contexto aberto durante toda a sessão do usuário.
/// </summary>
public sealed class RepositorioDispositivo(IDbContextFactory<MonitoramentoDbContext> fabrica) : IRepositorioDispositivo
{
    public async Task<List<Dispositivo>> ObterAtivosAsync(CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        return await db.Dispositivos
            .AsNoTrackingWithIdentityResolution()
            .Include(d => d.Servicos)
            .Where(d => d.Ativo)
            .ToListAsync(ct);
    }

    public async Task<List<Dispositivo>> ObterTodosAsync(CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        return await db.Dispositivos
            .AsNoTrackingWithIdentityResolution()
            .Include(d => d.Servicos)
            .OrderBy(d => d.Nome)
            .ToListAsync(ct);
    }

    public async Task<Dispositivo?> ObterPorIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        return await db.Dispositivos
            .AsNoTrackingWithIdentityResolution()
            .Include(d => d.Servicos)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<bool> EnderecoEmUsoAsync(string endereco, Guid? ignorarId = null, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var normalizado = endereco.Trim().ToLower();
        return await db.Dispositivos.AnyAsync(d => d.Endereco.ToLower() == normalizado && d.Id != ignorarId, ct);
    }

    public async Task AdicionarAsync(Dispositivo dispositivo, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        db.Dispositivos.Add(dispositivo);
        await db.SaveChangesAsync(ct);
    }

    public async Task AtualizarAsync(Dispositivo dispositivo, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var atual = await db.Dispositivos.Include(d => d.Servicos).FirstOrDefaultAsync(d => d.Id == dispositivo.Id, ct)
            ?? throw new InvalidOperationException("O dispositivo não existe mais no cadastro.");

        // Copia apenas os dados de cadastro; o estado é responsabilidade do serviço de monitoramento.
        atual.Nome = dispositivo.Nome;
        atual.Endereco = dispositivo.Endereco;
        atual.Descricao = dispositivo.Descricao;
        atual.IntervaloSegundos = dispositivo.IntervaloSegundos;
        atual.LimiteTentativas = dispositivo.LimiteTentativas;
        if (atual.Ativo != dispositivo.Ativo)
        {
            if (dispositivo.Ativo) atual.Reativar(); else atual.Desativar();
        }

        // Sincroniza os serviços TCP: remove os excluídos primeiro para liberar portas.
        var idsInformados = dispositivo.Servicos.Select(s => s.Id).ToHashSet();
        foreach (var removido in atual.Servicos.Where(s => !idsInformados.Contains(s.Id)).ToList())
        {
            atual.RemoverServico(removido);
            db.Servicos.Remove(removido);
        }
        await db.SaveChangesAsync(ct);

        foreach (var informado in dispositivo.Servicos)
        {
            var existente = atual.Servicos.FirstOrDefault(s => s.Id == informado.Id);
            if (existente is null)
            {
                var novo = atual.AdicionarServico(informado.Nome, informado.Porta);
                if (!informado.Ativo) novo.Desativar();
                db.Servicos.Add(novo);
                continue;
            }

            existente.Nome = informado.Nome;
            if (existente.Porta != informado.Porta)
            {
                // Outra porta é outro serviço: o estado volta a "desconhecido".
                existente.Porta = informado.Porta;
                existente.Desativar();
                existente.Reativar();
            }
            if (existente.Ativo != informado.Ativo)
            {
                if (informado.Ativo) existente.Reativar(); else existente.Desativar();
            }
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task RemoverAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        // A exclusão em cascata no banco remove serviços, verificações e alertas.
        await db.Dispositivos.Where(d => d.Id == id).ExecuteDeleteAsync(ct);
    }

    public async Task AtualizarEstadoAsync(Dispositivo dispositivo, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        await db.Dispositivos
            .Where(d => d.Id == dispositivo.Id && d.Ativo)
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, dispositivo.Status)
                .SetProperty(d => d.TentativasSemResposta, dispositivo.TentativasSemResposta)
                .SetProperty(d => d.UltimaVerificacaoEm, dispositivo.UltimaVerificacaoEm), ct);

        foreach (var servico in dispositivo.Servicos)
        {
            await db.Servicos
                .Where(s => s.Id == servico.Id && s.Ativo && s.Porta == servico.Porta)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, servico.Status)
                    .SetProperty(x => x.TentativasSemResposta, servico.TentativasSemResposta), ct);
        }

        await transacao.CommitAsync(ct);
    }

    public async Task<int> AplicarIntervaloATodosAsync(int intervaloSegundos, CancellationToken ct = default)
    {
        if (intervaloSegundos is < Dispositivo.IntervaloMinimoSegundos or > Dispositivo.IntervaloMaximoSegundos)
            throw new ArgumentOutOfRangeException(nameof(intervaloSegundos));

        await using var db = await fabrica.CreateDbContextAsync(ct);
        return await db.Dispositivos.ExecuteUpdateAsync(s => s.SetProperty(d => d.IntervaloSegundos, intervaloSegundos), ct);
    }
}
