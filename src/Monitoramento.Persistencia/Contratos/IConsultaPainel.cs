using Monitoramento.Persistencia.Consultas;

namespace Monitoramento.Persistencia.Contratos;

/// <summary>Consultas de leitura que alimentam a interface (painel, gráficos e histórico).</summary>
public interface IConsultaPainel
{
    /// <summary>Estado atual de todos os dispositivos (RF12).</summary>
    Task<List<ResumoDispositivo>> ObterResumoAsync(CancellationToken ct = default);

    /// <summary>Série de latência ICMP de um dispositivo no período (RF13).</summary>
    Task<List<PontoLatencia>> ObterLatenciaAsync(Guid dispositivoId, DateTime de, DateTime ate, CancellationToken ct = default);

    /// <summary>
    /// Percentual de disponibilidade do dispositivo (ICMP) ou, se <paramref name="servicoTcpId"/>
    /// for informado, do serviço TCP no período (RF14).
    /// </summary>
    Task<IndiceDisponibilidade> ObterDisponibilidadeAsync(Guid dispositivoId, DateTime de, DateTime ate,
        Guid? servicoTcpId = null, CancellationToken ct = default);

    /// <summary>Histórico de eventos e alertas com filtros (RF15).</summary>
    Task<List<EventoHistorico>> ObterHistoricoAsync(FiltroHistorico filtro, CancellationToken ct = default);

    /// <summary>Alertas de queda ainda em aberto.</summary>
    Task<List<EventoHistorico>> ObterAlertasAbertosAsync(CancellationToken ct = default);
}
