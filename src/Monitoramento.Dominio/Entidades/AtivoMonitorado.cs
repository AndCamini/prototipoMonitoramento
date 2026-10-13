using Monitoramento.Dominio.Enumeracoes;

namespace Monitoramento.Dominio.Entidades;

/// <summary>
/// Base comum a <see cref="Dispositivo"/> e <see cref="ServicoTcp"/>. Concentra a máquina de
/// estados da Figura 6, de modo que a regra do RF10 (tolerância a falhas consecutivas) não se
/// disperse pelo serviço de monitoramento e seja reaproveitada pelas duas entidades.
/// </summary>
public abstract class AtivoMonitorado
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
    public string Nome { get; set; } = string.Empty;
    public bool Ativo { get; protected set; } = true;
    public StatusAtivo Status { get; protected set; } = StatusAtivo.Desconhecido;
    public int TentativasSemResposta { get; protected set; }

    /// <summary>
    /// Status imediatamente anterior à última chamada de <see cref="AplicarResultado(ResultadoVerificacao, int)"/>.
    /// Não é persistido; serve ao <c>AvaliadorDeEstado</c> para decidir o tipo de alerta.
    /// </summary>
    public StatusAtivo StatusAnterior { get; private set; } = StatusAtivo.Desconhecido;

    /// <summary>
    /// Estado transitório "Verificando": já houve falha, mas a indisponibilidade
    /// ainda não foi confirmada (contador abaixo do limite).
    /// </summary>
    public bool EstaVerificando => Status != StatusAtivo.Offline && TentativasSemResposta > 0;

    /// <summary>
    /// Aplica o resultado de uma aferição conforme a máquina de estados (Figura 6).
    /// </summary>
    /// <returns><c>true</c> se o <see cref="Status"/> mudou.</returns>
    protected bool AplicarResultado(ResultadoVerificacao resultado, int limiteTentativas)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        StatusAnterior = Status;

        if (resultado.Sucesso)
        {
            // Desconhecido/Verificando/Offline --verificação com sucesso--> Online (zera contador)
            TentativasSemResposta = 0;
            Status = StatusAtivo.Online;
        }
        else if (Status != StatusAtivo.Offline)
        {
            // Desconhecido/Online --falha--> Verificando (incrementa contador);
            // ao atingir o limite (RF10) --> Offline.
            TentativasSemResposta = Math.Min(TentativasSemResposta + 1, short.MaxValue);
            if (TentativasSemResposta >= limiteTentativas)
                Status = StatusAtivo.Offline;
        }
        // Offline --verificação sem resposta--> Offline (nenhuma alteração)

        return Status != StatusAnterior;
    }

    /// <summary>Retira o ativo do ciclo de verificação, voltando ao estado inicial.</summary>
    public virtual void Desativar()
    {
        Ativo = false;
        ReiniciarEstado();
    }

    /// <summary>Devolve o ativo ao ciclo de verificação.</summary>
    public virtual void Reativar()
    {
        if (Ativo) return;
        Ativo = true;
        ReiniciarEstado();
    }

    protected void ReiniciarEstado()
    {
        Status = StatusAtivo.Desconhecido;
        StatusAnterior = StatusAtivo.Desconhecido;
        TentativasSemResposta = 0;
    }
}
