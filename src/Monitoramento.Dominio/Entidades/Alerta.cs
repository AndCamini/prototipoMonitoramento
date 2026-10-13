using Monitoramento.Dominio.Enumeracoes;

namespace Monitoramento.Dominio.Entidades;

/// <summary>
/// Transição de estado de um dispositivo ou serviço. Um alerta de queda permanece aberto
/// até o restabelecimento, permitindo medir a duração da indisponibilidade.
/// </summary>
public class Alerta
{
    public const int TamanhoMaximoDescricao = 300;

    protected Alerta() { }

    public Alerta(Guid dispositivoId, Guid? servicoTcpId, TipoEvento tipo, DateTime iniciadoEm, string? descricao)
    {
        DispositivoId = dispositivoId;
        ServicoTcpId = servicoTcpId;
        Tipo = tipo;
        IniciadoEm = iniciadoEm;
        Descricao = descricao is { Length: > TamanhoMaximoDescricao } ? descricao[..TamanhoMaximoDescricao] : descricao;

        // O evento de restabelecimento é pontual: nasce encerrado.
        if (tipo == TipoEvento.Restabelecimento)
            EncerradoEm = iniciadoEm;
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();
    public Guid DispositivoId { get; private set; }
    public Guid? ServicoTcpId { get; private set; }
    public TipoEvento Tipo { get; private set; }
    public DateTime IniciadoEm { get; private set; }
    public DateTime? EncerradoEm { get; private set; }
    public string? Descricao { get; private set; }

    /// <summary>Encerra o alerta no instante informado (restabelecimento).</summary>
    public void Encerrar(DateTime em)
    {
        if (EncerradoEm is not null) return;
        EncerradoEm = em < IniciadoEm ? IniciadoEm : em;
    }

    /// <summary>Complementa o texto apresentado ao usuário (ex.: duração da indisponibilidade).</summary>
    public void AcrescentarDescricao(string complemento)
    {
        var texto = string.IsNullOrWhiteSpace(Descricao) ? complemento : $"{Descricao} {complemento}";
        Descricao = texto.Length > TamanhoMaximoDescricao ? texto[..TamanhoMaximoDescricao] : texto;
    }

    public bool EstaAberto() => EncerradoEm is null;

    public TimeSpan? Duracao() => EncerradoEm - IniciadoEm;
}
