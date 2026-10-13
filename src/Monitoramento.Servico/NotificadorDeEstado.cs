using Microsoft.Extensions.Logging;
using Monitoramento.Dominio.Abstracoes;
using Monitoramento.Dominio.Entidades;

namespace Monitoramento.Servico;

/// <summary>
/// Implementação em memória do <see cref="INotificadorDeEstado"/>. Como o serviço e a interface
/// executam no mesmo processo, os componentes Blazor assinam diretamente os eventos.
/// </summary>
public sealed class NotificadorDeEstado(ILogger<NotificadorDeEstado> logger) : INotificadorDeEstado
{
    public event Func<EstadoAlterado, Task>? EstadoAlterado;
    public event Func<Task>? CicloConcluido;

    public Task PublicarAsync(Dispositivo dispositivo, Alerta? alerta)
    {
        var evento = new EstadoAlterado(dispositivo.Id, dispositivo.Nome, alerta?.ServicoTcpId, alerta?.Tipo,
            alerta?.Descricao, alerta?.IniciadoEm ?? dispositivo.UltimaVerificacaoEm ?? DateTime.UtcNow);
        return Disparar(EstadoAlterado, h => h(evento));
    }

    public Task PublicarCicloConcluidoAsync() => Disparar(CicloConcluido, h => h());

    // Um assinante com falha (ex.: navegador desconectado) não pode interromper o monitoramento.
    private async Task Disparar<T>(T? evento, Func<T, Task> invocar) where T : Delegate
    {
        if (evento is null) return;
        foreach (var assinante in evento.GetInvocationList().Cast<T>())
        {
            try
            {
                await invocar(assinante);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Falha ao notificar um assinante do painel.");
            }
        }
    }
}
