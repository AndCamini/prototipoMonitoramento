using Microsoft.EntityFrameworkCore;
using Monitoramento.Dominio.Enumeracoes;
using Monitoramento.Persistencia.Consultas;
using Monitoramento.Persistencia.Contratos;

namespace Monitoramento.Persistencia.Repositorios;

/// <summary>Consultas de leitura que alimentam o painel, os gráficos e o histórico.</summary>
public sealed class ConsultaPainel(IDbContextFactory<MonitoramentoDbContext> fabrica, TimeProvider relogio) : IConsultaPainel
{
    /// <summary>Quantidade máxima de pontos devolvidos para o gráfico de latência.</summary>
    public const int MaximoPontosGrafico = 720;

    public async Task<List<ResumoDispositivo>> ObterResumoAsync(CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var linhas = await db.Dispositivos.AsNoTracking()
            .OrderBy(d => d.Nome)
            .Select(d => new
            {
                d.Id, d.Nome, d.Endereco, d.Descricao, d.Ativo, d.Status, d.TentativasSemResposta,
                d.LimiteTentativas, d.IntervaloSegundos, d.UltimaVerificacaoEm,
                UltimaLatencia = db.Verificacoes
                    .Where(v => v.DispositivoId == d.Id && v.Tipo == TipoVerificacao.Icmp)
                    .OrderByDescending(v => v.DataHora)
                    .Select(v => v.LatenciaMs)
                    .FirstOrDefault(),
                Servicos = d.Servicos
                    .OrderBy(s => s.Porta)
                    .Select(s => new ResumoServico(s.Id, s.Nome, s.Porta, s.Ativo, s.Status, s.TentativasSemResposta))
                    .ToList()
            })
            .ToListAsync(ct);

        return linhas.Select(l => new ResumoDispositivo(
            l.Id, l.Nome, l.Endereco, l.Descricao, l.Ativo, l.Status, l.TentativasSemResposta,
            l.LimiteTentativas, l.IntervaloSegundos, l.UltimaVerificacaoEm, l.UltimaLatencia, l.Servicos)).ToList();
    }

    public async Task<List<PontoLatencia>> ObterLatenciaAsync(Guid dispositivoId, DateTime de, DateTime ate, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        var pontos = await db.Verificacoes.AsNoTracking()
            .Where(v => v.DispositivoId == dispositivoId && v.Tipo == TipoVerificacao.Icmp
                        && v.DataHora >= de && v.DataHora <= ate)
            .OrderBy(v => v.DataHora)
            .Select(v => new PontoLatencia(v.DataHora, v.Sucesso ? v.LatenciaMs : null))
            .ToListAsync(ct);

        return Reduzir(pontos, de, ate, MaximoPontosGrafico);
    }

    /// <summary>
    /// Agrupa a série em faixas de tempo de mesmo tamanho quando há mais pontos do que o gráfico
    /// comporta. A latência de cada faixa é a média das respostas; se nenhuma aferição da faixa
    /// obteve resposta, o ponto é nulo (indisponibilidade).
    /// </summary>
    internal static List<PontoLatencia> Reduzir(List<PontoLatencia> pontos, DateTime de, DateTime ate, int maximo)
    {
        if (pontos.Count <= maximo || ate <= de) return pontos;

        var largura = (ate - de) / maximo;
        return pontos
            .GroupBy(p => (long)((p.Instante - de) / largura))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var respostas = g.Where(p => p.LatenciaMs.HasValue).Select(p => p.LatenciaMs!.Value).ToList();
                int? media = respostas.Count > 0 ? (int)Math.Round(respostas.Average()) : null;
                return new PontoLatencia(de + largura * g.Key + largura / 2, media);
            })
            .ToList();
    }

    public async Task<IndiceDisponibilidade> ObterDisponibilidadeAsync(Guid dispositivoId, DateTime de, DateTime ate,
        Guid? servicoTcpId = null, CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);

        var verificacoes = db.Verificacoes.AsNoTracking()
            .Where(v => v.DispositivoId == dispositivoId && v.DataHora >= de && v.DataHora <= ate);
        verificacoes = servicoTcpId is null
            ? verificacoes.Where(v => v.Tipo == TipoVerificacao.Icmp)
            : verificacoes.Where(v => v.ServicoTcpId == servicoTcpId);

        var totais = await verificacoes
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Sucessos = g.Count(v => v.Sucesso),
                Media = g.Average(v => (double?)v.LatenciaMs)
            })
            .FirstOrDefaultAsync(ct);

        var quedas = await db.Alertas.AsNoTracking()
            .Where(a => a.DispositivoId == dispositivoId && a.ServicoTcpId == servicoTcpId
                        && a.Tipo == TipoEvento.Queda
                        && a.IniciadoEm <= ate && (a.EncerradoEm == null || a.EncerradoEm >= de))
            .Select(a => new { a.IniciadoEm, a.EncerradoEm })
            .ToListAsync(ct);

        var agora = relogio.GetUtcNow().UtcDateTime;
        var limiteSuperior = ate < agora ? ate : agora;
        var tempoIndisponivel = quedas.Aggregate(TimeSpan.Zero, (soma, q) =>
        {
            var inicio = q.IniciadoEm > de ? q.IniciadoEm : de;
            var fim = q.EncerradoEm ?? limiteSuperior;
            if (fim > ate) fim = ate;
            return fim > inicio ? soma + (fim - inicio) : soma;
        });

        var total = totais?.Total ?? 0;
        var sucessos = totais?.Sucessos ?? 0;
        var percentual = total == 0 ? 0m : Math.Round(sucessos * 100m / total, 2);
        return new IndiceDisponibilidade(percentual, total, sucessos, tempoIndisponivel, quedas.Count, totais?.Media);
    }

    public async Task<List<EventoHistorico>> ObterHistoricoAsync(FiltroHistorico filtro, CancellationToken ct = default)
    {
        var tipos = filtro.Tipos is { Count: > 0 } ? filtro.Tipos : Enum.GetValues<TipoOcorrencia>();
        var querQueda = tipos.Contains(TipoOcorrencia.Queda);
        var querRestabelecimento = tipos.Contains(TipoOcorrencia.Restabelecimento);
        var querFalha = tipos.Contains(TipoOcorrencia.FalhaDeVerificacao);
        var querSucesso = tipos.Contains(TipoOcorrencia.VerificacaoComSucesso);

        await using var db = await fabrica.CreateDbContextAsync(ct);
        var resultado = new List<EventoHistorico>();

        if (querQueda || querRestabelecimento)
        {
            var alertas = await (
                    from a in db.Alertas.AsNoTracking()
                    join d in db.Dispositivos on a.DispositivoId equals d.Id
                    from s in db.Servicos.Where(s => s.Id == a.ServicoTcpId).DefaultIfEmpty()
                    where (filtro.DispositivoId == null || a.DispositivoId == filtro.DispositivoId)
                          && a.IniciadoEm >= filtro.De && a.IniciadoEm <= filtro.Ate
                          && ((querQueda && a.Tipo == TipoEvento.Queda) || (querRestabelecimento && a.Tipo == TipoEvento.Restabelecimento))
                    orderby a.IniciadoEm descending
                    select new EventoHistorico(
                        a.Id, a.IniciadoEm,
                        a.Tipo == TipoEvento.Queda ? TipoOcorrencia.Queda : TipoOcorrencia.Restabelecimento,
                        d.Id, d.Nome, s == null ? null : s.Nome, s == null ? null : s.Porta,
                        a.Descricao, null, a.EncerradoEm))
                .Take(filtro.Limite)
                .ToListAsync(ct);
            resultado.AddRange(alertas);
        }

        if (querFalha || querSucesso)
        {
            var verificacoes = await (
                    from v in db.Verificacoes.AsNoTracking()
                    join d in db.Dispositivos on v.DispositivoId equals d.Id
                    from s in db.Servicos.Where(s => s.Id == v.ServicoTcpId).DefaultIfEmpty()
                    where (filtro.DispositivoId == null || v.DispositivoId == filtro.DispositivoId)
                          && v.DataHora >= filtro.De && v.DataHora <= filtro.Ate
                          && ((querFalha && !v.Sucesso) || (querSucesso && v.Sucesso))
                    orderby v.DataHora descending
                    select new EventoHistorico(
                        v.Id, v.DataHora,
                        v.Sucesso ? TipoOcorrencia.VerificacaoComSucesso : TipoOcorrencia.FalhaDeVerificacao,
                        d.Id, d.Nome, s == null ? null : s.Nome, s == null ? null : s.Porta,
                        v.MensagemErro, v.LatenciaMs, null))
                .Take(filtro.Limite)
                .ToListAsync(ct);
            resultado.AddRange(verificacoes);
        }

        return resultado.OrderByDescending(e => e.Instante).Take(filtro.Limite).ToList();
    }

    public async Task<List<EventoHistorico>> ObterAlertasAbertosAsync(CancellationToken ct = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(ct);
        return await (
                from a in db.Alertas.AsNoTracking()
                join d in db.Dispositivos on a.DispositivoId equals d.Id
                from s in db.Servicos.Where(s => s.Id == a.ServicoTcpId).DefaultIfEmpty()
                where a.Tipo == TipoEvento.Queda && a.EncerradoEm == null
                orderby a.IniciadoEm descending
                select new EventoHistorico(
                    a.Id, a.IniciadoEm, TipoOcorrencia.Queda, d.Id, d.Nome,
                    s == null ? null : s.Nome, s == null ? null : s.Porta, a.Descricao, null, null))
            .ToListAsync(ct);
    }
}
