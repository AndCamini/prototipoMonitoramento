namespace Monitoramento.Dominio.Entidades;

/// <summary>
/// Porta TCP verificada em um dispositivo. Repete a estrutura de estado do dispositivo,
/// pois um serviço pode estar indisponível em um equipamento que continua respondendo
/// na camada de rede. Não existe fora do dispositivo ao qual pertence (composição).
/// </summary>
public class ServicoTcp : AtivoMonitorado
{
    public const int PortaMinima = 1;
    public const int PortaMaxima = 65535;

    private int _porta;

    // Construtor usado pelo Entity Framework Core.
    protected ServicoTcp() { }

    internal ServicoTcp(Dispositivo dispositivo, string nome, int porta)
    {
        Dispositivo = dispositivo;
        DispositivoId = dispositivo.Id;
        Nome = nome;
        Porta = porta;
    }

    public Guid DispositivoId { get; private set; }
    public Dispositivo Dispositivo { get; private set; } = null!;

    public int Porta
    {
        get => _porta;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, PortaMinima);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, PortaMaxima);
            _porta = value;
        }
    }

    /// <summary>
    /// Aplica o resultado da tentativa de conexão TCP, usando o limite de tentativas
    /// configurado no dispositivo hospedeiro.
    /// </summary>
    /// <returns><c>true</c> se houve transição de estado.</returns>
    public bool AplicarResultado(ResultadoVerificacao resultado) =>
        AplicarResultado(resultado, Dispositivo.LimiteTentativas);

    public string DescricaoStatus => Dispositivo.DescreverStatus(Ativo, Status, TentativasSemResposta);
}
