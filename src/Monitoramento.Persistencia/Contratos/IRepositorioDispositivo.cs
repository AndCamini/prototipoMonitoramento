using Monitoramento.Dominio.Entidades;

namespace Monitoramento.Persistencia.Contratos;

/// <summary>Manutenção do cadastro e leitura dos ativos monitorados.</summary>
public interface IRepositorioDispositivo
{
    /// <summary>Dispositivos com monitoramento ativo, com seus serviços TCP.</summary>
    Task<List<Dispositivo>> ObterAtivosAsync(CancellationToken ct = default);

    Task<List<Dispositivo>> ObterTodosAsync(CancellationToken ct = default);

    Task<Dispositivo?> ObterPorIdAsync(Guid id, CancellationToken ct = default);

    Task<bool> EnderecoEmUsoAsync(string endereco, Guid? ignorarId = null, CancellationToken ct = default);

    Task AdicionarAsync(Dispositivo dispositivo, CancellationToken ct = default);

    /// <summary>Grava as alterações de cadastro de um dispositivo obtido por <see cref="ObterPorIdAsync"/>.</summary>
    Task AtualizarAsync(Dispositivo dispositivo, CancellationToken ct = default);

    Task RemoverAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Grava somente as colunas de estado (status, contador e última verificação) do dispositivo
    /// e de seus serviços, sem sobrescrever alterações de cadastro feitas em paralelo pela interface.
    /// </summary>
    Task AtualizarEstadoAsync(Dispositivo dispositivo, CancellationToken ct = default);

    /// <summary>Aplica o mesmo intervalo de verificação a todos os dispositivos (RF05).</summary>
    Task<int> AplicarIntervaloATodosAsync(int intervaloSegundos, CancellationToken ct = default);
}
