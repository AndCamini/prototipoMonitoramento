using Monitoramento.Dominio.Enumeracoes;

namespace Monitoramento.Dominio.Entidades;

/// <summary>
/// Ativo de rede monitorado (servidor, roteador, impressora, switch...). Elemento central
/// do domínio: guarda os parâmetros da própria verificação e o estado corrente.
/// </summary>
public class Dispositivo : AtivoMonitorado
{
    public const int IntervaloMinimoSegundos = 10;
    public const int IntervaloMaximoSegundos = 3600;
    public const int LimiteTentativasMinimo = 1;
    public const int LimiteTentativasMaximo = 10;

    private int _intervaloSegundos = 60;
    private int _limiteTentativas = 3;
    private readonly List<ServicoTcp> _servicos = [];

    // Construtor usado pelo Entity Framework Core.
    protected Dispositivo() { }

    public Dispositivo(string nome, string endereco, string? descricao = null,
        int intervaloSegundos = 60, int limiteTentativas = 3, DateTime? criadoEm = null)
    {
        Nome = nome;
        Endereco = endereco;
        Descricao = descricao;
        IntervaloSegundos = intervaloSegundos;
        LimiteTentativas = limiteTentativas;
        CriadoEm = criadoEm ?? DateTime.UtcNow;
    }

    public string Endereco { get; set; } = string.Empty;
    public string? Descricao { get; set; }

    /// <summary>Intervalo entre verificações, entre 10 e 3600 segundos (RF05).</summary>
    public int IntervaloSegundos
    {
        get => _intervaloSegundos;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, IntervaloMinimoSegundos);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, IntervaloMaximoSegundos);
            _intervaloSegundos = value;
        }
    }

    /// <summary>Falhas consecutivas exigidas antes de declarar indisponibilidade (RF10).</summary>
    public int LimiteTentativas
    {
        get => _limiteTentativas;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, LimiteTentativasMinimo);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, LimiteTentativasMaximo);
            _limiteTentativas = value;
        }
    }

    public DateTime? UltimaVerificacaoEm { get; private set; }
    public DateTime CriadoEm { get; private set; }

    public IReadOnlyCollection<ServicoTcp> Servicos => _servicos;

    /// <summary>
    /// Folga aceita pelo agendamento: o ciclo inicia alguns milissegundos antes do instante exato
    /// em que o intervalo se completa, e sem a folga a verificação seria adiada por um pulso inteiro.
    /// </summary>
    public static readonly TimeSpan ToleranciaAgendamento = TimeSpan.FromSeconds(1);

    /// <summary>Decide se o intervalo configurado já transcorreu desde a última aferição.</summary>
    public bool DeveVerificar(DateTime agora) =>
        Ativo && (UltimaVerificacaoEm is null ||
                  agora - UltimaVerificacaoEm.Value >= TimeSpan.FromSeconds(IntervaloSegundos) - ToleranciaAgendamento);

    /// <summary>
    /// Atualiza o contador de tentativas e o status a partir do resultado da verificação ICMP.
    /// </summary>
    /// <returns><c>true</c> se houve transição de estado.</returns>
    public bool AplicarResultado(ResultadoVerificacao resultado)
    {
        var houveTransicao = AplicarResultado(resultado, LimiteTentativas);
        UltimaVerificacaoEm = resultado.AferidoEm;
        return houveTransicao;
    }

    public ServicoTcp AdicionarServico(string nome, int porta)
    {
        if (_servicos.Any(s => s.Porta == porta))
            throw new InvalidOperationException($"A porta {porta} já está cadastrada neste dispositivo.");

        var servico = new ServicoTcp(this, nome, porta);
        _servicos.Add(servico);
        return servico;
    }

    public void RemoverServico(ServicoTcp servico) => _servicos.Remove(servico);

    public override void Desativar()
    {
        base.Desativar();
        UltimaVerificacaoEm = null;
    }

    public override void Reativar()
    {
        base.Reativar();
        // Força a verificação no ciclo seguinte.
        UltimaVerificacaoEm = null;
    }

    /// <summary>Descrição do estado em linguagem acessível (RNF08).</summary>
    public string DescricaoStatus => DescreverStatus(Ativo, Status, TentativasSemResposta);

    public static string DescreverStatus(bool ativo, StatusAtivo status, int tentativasSemResposta) =>
        !ativo ? "Monitoramento desativado"
        : status == StatusAtivo.Offline ? "Indisponível"
        : tentativasSemResposta > 0 ? "Verificando (sem resposta)"
        : status == StatusAtivo.Online ? "Disponível"
        : "Aguardando primeira verificação";
}
