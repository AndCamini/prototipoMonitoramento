using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Monitoramento.Dominio;
using Monitoramento.Dominio.Enumeracoes;

namespace Monitoramento.Servico.Verificadores;

/// <summary>
/// Verifica se um serviço aceita conexões na porta cadastrada, distinguindo conexão aceita,
/// recusada ou expirada por tempo limite (RF07).
/// </summary>
public sealed class VerificadorTcp(IOptions<MonitoramentoOptions> opcoes, TimeProvider relogio) : IVerificador
{
    private readonly int _timeoutMs = Math.Max(100, opcoes.Value.TimeoutTcpMs);

    public TipoVerificacao Tipo => TipoVerificacao.Tcp;

    public async Task<ResultadoVerificacao> VerificarAsync(AlvoVerificacao alvo, CancellationToken ct = default)
    {
        if (alvo.Porta is not { } porta)
            throw new ArgumentException("A verificação TCP exige uma porta.", nameof(alvo));

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(_timeoutMs);
        var inicio = relogio.GetUtcNow().UtcDateTime;
        var cronometro = Stopwatch.StartNew();

        try
        {
            using var cliente = new TcpClient();
            await cliente.ConnectAsync(alvo.Endereco, porta, limite.Token);
            return ResultadoVerificacao.Ok((int)cronometro.ElapsedMilliseconds, inicio);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return ResultadoVerificacao.Falha($"Tempo limite esgotado: a porta {porta} não respondeu em {_timeoutMs} ms", inicio);
        }
        catch (SocketException ex)
        {
            return ResultadoVerificacao.Falha(ex.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => $"Conexão recusada na porta {porta} (serviço parado ou bloqueado)",
                SocketError.TimedOut => $"Tempo limite esgotado: a porta {porta} não respondeu",
                SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain =>
                    "Endereço não encontrado (falha na resolução de nome)",
                SocketError.HostUnreachable or SocketError.NetworkUnreachable => "Destino inalcançável",
                _ => $"Falha na conexão com a porta {porta}: {ex.Message}"
            }, inicio);
        }
    }
}
