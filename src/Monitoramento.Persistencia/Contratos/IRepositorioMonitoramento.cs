using Monitoramento.Dominio.Entidades;

namespace Monitoramento.Persistencia.Contratos;

/// <summary>Gravação dos eventos de monitoramento (verificações e alertas).</summary>
public interface IRepositorioMonitoramento
{
    Task SalvarVerificacaoAsync(Verificacao verificacao, CancellationToken ct = default);

    Task SalvarVerificacoesAsync(IEnumerable<Verificacao> verificacoes, CancellationToken ct = default);

    Task SalvarAlertaAsync(Alerta alerta, CancellationToken ct = default);

    /// <summary>Alerta de queda ainda não encerrado do dispositivo (ou do serviço, se informado).</summary>
    Task<Alerta?> ObterAlertaAbertoAsync(Guid dispositivoId, Guid? servicoTcpId, CancellationToken ct = default);

    /// <summary>Encerra o alerta aberto do ativo e grava o evento de restabelecimento.</summary>
    Task<Alerta?> EncerrarAlertaAbertoAsync(Guid dispositivoId, Guid? servicoTcpId, DateTime em, CancellationToken ct = default);

    /// <summary>Encerra todos os alertas abertos do dispositivo (ex.: monitoramento desativado).</summary>
    Task EncerrarAlertasAbertosAsync(Guid dispositivoId, DateTime em, CancellationToken ct = default);

    Task<List<Alerta>> ObterHistoricoAsync(Guid? dispositivoId, DateTime de, DateTime ate, CancellationToken ct = default);
}
