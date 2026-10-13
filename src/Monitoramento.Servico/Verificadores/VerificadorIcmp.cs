using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Monitoramento.Dominio;
using Monitoramento.Dominio.Enumeracoes;

namespace Monitoramento.Servico.Verificadores;

/// <summary>Verificação de alcançabilidade e latência pelo par ICMP Echo Request/Echo Reply.</summary>
public sealed class VerificadorIcmp(IOptions<MonitoramentoOptions> opcoes, TimeProvider relogio) : IVerificador
{
    private static readonly byte[] Carga = new byte[32];
    private readonly int _timeoutMs = Math.Max(100, opcoes.Value.TimeoutIcmpMs);

    public TipoVerificacao Tipo => TipoVerificacao.Icmp;

    public async Task<ResultadoVerificacao> VerificarAsync(AlvoVerificacao alvo, CancellationToken ct = default)
    {
        var inicio = relogio.GetUtcNow().UtcDateTime;
        try
        {
            using var ping = new Ping();
            var resposta = await ping.SendPingAsync(alvo.Endereco, TimeSpan.FromMilliseconds(_timeoutMs), Carga,
                new PingOptions(ttl: 64, dontFragment: true), ct);

            return resposta.Status == IPStatus.Success
                ? ResultadoVerificacao.Ok((int)resposta.RoundtripTime, inicio)
                : ResultadoVerificacao.Falha(Descrever(resposta.Status), inicio);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ResultadoVerificacao.Falha(DescreverExcecao(ex), inicio);
        }
    }

    private string Descrever(IPStatus status) => status switch
    {
        IPStatus.TimedOut => $"Sem resposta ao ping em {_timeoutMs} ms",
        IPStatus.DestinationHostUnreachable => "Destino inalcançável (host)",
        IPStatus.DestinationNetworkUnreachable => "Destino inalcançável (rede)",
        IPStatus.DestinationUnreachable => "Destino inalcançável",
        IPStatus.TtlExpired or IPStatus.TimeExceeded => "Tempo de vida (TTL) expirado no caminho",
        _ => $"Falha no ping ({status})"
    };

    private static string DescreverExcecao(Exception ex) => ex.GetBaseException() switch
    {
        SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain } =>
            "Endereço não encontrado (falha na resolução de nome)",
        var e => $"Falha no ping: {e.Message}"
    };
}
