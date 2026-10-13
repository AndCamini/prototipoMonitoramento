namespace Monitoramento.Dominio;

/// <summary>
/// Desfecho de uma aferição ICMP ou TCP, produzido por um <c>IVerificador</c>
/// e submetido às entidades por meio de <c>AplicarResultado</c>.
/// </summary>
/// <param name="Sucesso">Indica se houve resposta (Echo Reply ou conexão aceita).</param>
/// <param name="LatenciaMs">Tempo de resposta em milissegundos, quando houve resposta.</param>
/// <param name="MensagemErro">Descrição da falha, quando houver.</param>
/// <param name="AferidoEm">Instante (UTC) em que a aferição foi iniciada (envio do Echo Request ou da tentativa de conexão).</param>
public sealed record ResultadoVerificacao(bool Sucesso, int? LatenciaMs, string? MensagemErro, DateTime AferidoEm)
{
    public static ResultadoVerificacao Ok(int latenciaMs, DateTime aferidoEm) =>
        new(true, latenciaMs, null, aferidoEm);

    public static ResultadoVerificacao Falha(string mensagem, DateTime aferidoEm) =>
        new(false, null, mensagem, aferidoEm);
}
