using Monitoramento.Dominio.Enumeracoes;

namespace Monitoramento.Dominio.Entidades;

/// <summary>
/// Resultado individual de uma aferição. O acúmulo dessas instâncias forma as séries
/// históricas de latência e os índices de disponibilidade.
/// </summary>
public class Verificacao
{
    public const int TamanhoMaximoMensagem = 300;

    protected Verificacao() { }

    private Verificacao(Guid dispositivoId, Guid? servicoTcpId, TipoVerificacao tipo, ResultadoVerificacao resultado)
    {
        DispositivoId = dispositivoId;
        ServicoTcpId = servicoTcpId;
        Tipo = tipo;
        DataHora = resultado.AferidoEm;
        Sucesso = resultado.Sucesso;
        LatenciaMs = resultado.Sucesso ? resultado.LatenciaMs : null;
        MensagemErro = Truncar(resultado.MensagemErro);
    }

    public static Verificacao DeIcmp(Dispositivo dispositivo, ResultadoVerificacao resultado) =>
        new(dispositivo.Id, null, TipoVerificacao.Icmp, resultado);

    public static Verificacao DeTcp(ServicoTcp servico, ResultadoVerificacao resultado) =>
        new(servico.DispositivoId, servico.Id, TipoVerificacao.Tcp, resultado);

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid DispositivoId { get; private set; }
    public Guid? ServicoTcpId { get; private set; }
    public DateTime DataHora { get; private set; }
    public TipoVerificacao Tipo { get; private set; }
    public bool Sucesso { get; private set; }
    public int? LatenciaMs { get; private set; }
    public string? MensagemErro { get; private set; }

    private static string? Truncar(string? texto) =>
        texto is { Length: > TamanhoMaximoMensagem } ? texto[..TamanhoMaximoMensagem] : texto;
}
