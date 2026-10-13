namespace Monitoramento.Servico;

/// <summary>
/// Parâmetros de execução do serviço de monitoramento, lidos da seção "Monitoramento" do
/// appsettings.json, de modo que possam ser ajustados sem recompilação.
/// </summary>
public sealed class MonitoramentoOptions
{
    public const string Secao = "Monitoramento";

    /// <summary>Intervalo padrão entre verificações sugerido no cadastro (RF05).</summary>
    public int IntervaloPadraoSegundos { get; set; } = 60;

    /// <summary>Falhas consecutivas padrão antes de declarar indisponibilidade (RF10).</summary>
    public int LimiteTentativasPadrao { get; set; } = 3;

    /// <summary>Tempo máximo de espera pelo Echo Reply.</summary>
    public int TimeoutIcmpMs { get; set; } = 1000;

    /// <summary>Tempo máximo de espera pelo estabelecimento da conexão TCP.</summary>
    public int TimeoutTcpMs { get; set; } = 2000;

    /// <summary>Número máximo de dispositivos verificados ao mesmo tempo (RNF04/RNF05).</summary>
    public int MaxVerificacoesSimultaneas { get; set; } = 20;

    /// <summary>
    /// Periodicidade com que o worker confere quais dispositivos já atingiram o próprio intervalo.
    /// </summary>
    public int PulsoSegundos { get; set; } = 5;
}
