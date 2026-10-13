using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Monitoramento.Dominio;
using Monitoramento.Dominio.Abstracoes;
using Monitoramento.Dominio.Entidades;
using Monitoramento.Dominio.Enumeracoes;
using Monitoramento.Persistencia.Contratos;
using Monitoramento.Servico.Verificadores;

namespace Monitoramento.Servico;

/// <summary>
/// Serviço de monitoramento (Worker Service). Executa em segundo plano o ciclo de verificação
/// descrito nas Figuras 3 e 5: obtém os dispositivos ativos, dispara as aferições de forma
/// assíncrona e paralela, aplica os resultados, decide sobre alertas, grava e notifica o painel.
/// </summary>
public sealed class MonitoramentoWorker(
    IServiceScopeFactory escopos,
    IOptions<MonitoramentoOptions> opcoes,
    IEnumerable<IVerificador> verificadores,
    AvaliadorDeEstado avaliador,
    INotificadorDeEstado notificador,
    TimeProvider relogio,
    ILogger<MonitoramentoWorker> logger) : BackgroundService
{
    private readonly MonitoramentoOptions _opcoes = opcoes.Value;
    private readonly IVerificador _icmp = verificadores.First(v => v.Tipo == TipoVerificacao.Icmp);
    private readonly IVerificador _tcp = verificadores.First(v => v.Tipo == TipoVerificacao.Tcp);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("Serviço de monitoramento iniciado (pulso de {Pulso}s, até {Max} verificações simultâneas).",
            _opcoes.PulsoSegundos, _opcoes.MaxVerificacoesSimultaneas);

        using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _opcoes.PulsoSegundos)), relogio);
        do
        {
            try
            {
                await ExecutarCicloAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // RNF09: falha temporária (banco ou rede) é registrada e o ciclo seguinte é retomado.
                logger.LogError(ex, "Falha no ciclo de verificação; nova tentativa no próximo ciclo.");
            }
        } while (await AguardarProximoCicloAsync(temporizador, ct));

        logger.LogInformation("Serviço de monitoramento encerrado.");
    }

    private static async Task<bool> AguardarProximoCicloAsync(PeriodicTimer temporizador, CancellationToken ct)
    {
        try
        {
            return await temporizador.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>Um ciclo: verifica, em paralelo, todos os dispositivos cujo intervalo já transcorreu.</summary>
    public async Task ExecutarCicloAsync(CancellationToken ct)
    {
        await using var escopo = escopos.CreateAsyncScope();
        var repositorioDispositivo = escopo.ServiceProvider.GetRequiredService<IRepositorioDispositivo>();
        var repositorioMonitoramento = escopo.ServiceProvider.GetRequiredService<IRepositorioMonitoramento>();

        var agora = relogio.GetUtcNow().UtcDateTime;
        var pendentes = (await repositorioDispositivo.ObterAtivosAsync(ct)).Where(d => d.DeveVerificar(agora)).ToList();
        if (pendentes.Count == 0) return;

        var paralelismo = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, _opcoes.MaxVerificacoesSimultaneas),
            CancellationToken = ct
        };

        // RNF04: a lentidão ou indisponibilidade de um dispositivo não atrasa os demais.
        await Parallel.ForEachAsync(pendentes, paralelismo, async (dispositivo, token) =>
        {
            try
            {
                await VerificarDispositivoAsync(dispositivo, repositorioDispositivo, repositorioMonitoramento, token);
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                logger.LogError(ex, "Falha ao verificar {Dispositivo} ({Endereco}).", dispositivo.Nome, dispositivo.Endereco);
            }
        });

        await notificador.PublicarCicloConcluidoAsync();
    }

    private async Task VerificarDispositivoAsync(Dispositivo dispositivo, IRepositorioDispositivo repositorioDispositivo,
        IRepositorioMonitoramento repositorioMonitoramento, CancellationToken ct)
    {
        // 1. Verificação ICMP do host.
        var resultadoIcmp = await _icmp.VerificarAsync(new AlvoVerificacao(dispositivo.Endereco), ct);

        // 2. Havendo resposta, tentativa de conexão em cada porta cadastrada (em paralelo).
        var resultadosTcp = new List<(ServicoTcp Servico, ResultadoVerificacao Resultado)>();
        if (resultadoIcmp.Sucesso)
        {
            var servicos = dispositivo.Servicos.Where(s => s.Ativo).ToList();
            var resultados = await Task.WhenAll(servicos.Select(s =>
                _tcp.VerificarAsync(new AlvoVerificacao(dispositivo.Endereco, s.Porta), ct)));
            resultadosTcp.AddRange(servicos.Zip(resultados));
        }

        // 3. Aplicação dos resultados às entidades e avaliação de alertas.
        var verificacoes = new List<Verificacao> { Verificacao.DeIcmp(dispositivo, resultadoIcmp) };
        var transicoes = new List<Alerta?>();

        var dispositivoMudou = dispositivo.AplicarResultado(resultadoIcmp);
        if (dispositivoMudou) transicoes.Add(avaliador.Avaliar(dispositivo, resultadoIcmp));

        foreach (var (servico, resultado) in resultadosTcp)
        {
            verificacoes.Add(Verificacao.DeTcp(servico, resultado));
            if (servico.AplicarResultado(resultado))
                transicoes.Add(avaliador.Avaliar(servico, resultado));
        }

        // 4. O registro das verificações é sempre persistido; o alerta, apenas quando houve transição.
        await repositorioMonitoramento.SalvarVerificacoesAsync(verificacoes, ct);
        await repositorioDispositivo.AtualizarEstadoAsync(dispositivo, ct);

        foreach (var alerta in transicoes)
        {
            if (alerta is not null)
            {
                if (alerta.Tipo == TipoEvento.Restabelecimento)
                {
                    var queda = await repositorioMonitoramento.EncerrarAlertaAbertoAsync(
                        alerta.DispositivoId, alerta.ServicoTcpId, alerta.IniciadoEm, ct);
                    if (queda?.Duracao() is { } duracao)
                        alerta.AcrescentarDescricao($"Indisponível por {FormatarDuracao(duracao)}.");
                }

                await repositorioMonitoramento.SalvarAlertaAsync(alerta, ct);
                LogarAlerta(dispositivo, alerta);
            }

            // 5. O painel é notificado de toda mudança de estado.
            await notificador.PublicarAsync(dispositivo, alerta);
        }
    }

    private void LogarAlerta(Dispositivo dispositivo, Alerta alerta)
    {
        if (alerta.Tipo == TipoEvento.Queda)
            logger.LogWarning("ALERTA DE QUEDA [{Dispositivo}]: {Descricao}", dispositivo.Nome, alerta.Descricao);
        else
            logger.LogInformation("RESTABELECIMENTO [{Dispositivo}]: {Descricao}", dispositivo.Nome, alerta.Descricao);
    }

    public static string FormatarDuracao(TimeSpan duracao) => duracao switch
    {
        { TotalDays: >= 1 } => $"{(int)duracao.TotalDays}d {duracao.Hours}h {duracao.Minutes}min",
        { TotalHours: >= 1 } => $"{(int)duracao.TotalHours}h {duracao.Minutes}min",
        { TotalMinutes: >= 1 } => $"{(int)duracao.TotalMinutes}min {duracao.Seconds}s",
        _ => $"{Math.Max(0, (int)duracao.TotalSeconds)}s"
    };
}
