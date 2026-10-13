using Monitoramento.Dominio.Entidades;

namespace Monitoramento.Dominio.Abstracoes;

/// <summary>
/// Publica as mudanças de estado detectadas pelo serviço de monitoramento para a camada
/// de apresentação, permitindo que o painel se atualize sem consultas periódicas ao banco
/// (RF12). Declarado no domínio para que o serviço de monitoramento e a interface web
/// compartilhem o contrato sem que um dependa do outro.
/// </summary>
public interface INotificadorDeEstado
{
    /// <summary>Disparado a cada transição de estado de um dispositivo ou serviço.</summary>
    event Func<EstadoAlterado, Task>? EstadoAlterado;

    /// <summary>Disparado ao fim de cada ciclo de verificação (novas latências disponíveis).</summary>
    event Func<Task>? CicloConcluido;

    Task PublicarAsync(Dispositivo dispositivo, Alerta? alerta);

    Task PublicarCicloConcluidoAsync();
}

/// <summary>Dados do evento de mudança de estado.</summary>
public sealed record EstadoAlterado(
    Guid DispositivoId,
    string NomeDispositivo,
    Guid? ServicoTcpId,
    Enumeracoes.TipoEvento? TipoEvento,
    string? Descricao,
    DateTime Instante);
