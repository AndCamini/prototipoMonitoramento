using Monitoramento.Dominio.Enumeracoes;

namespace Monitoramento.Persistencia.Consultas;

/// <summary>Estado atual de um dispositivo, como exibido no painel.</summary>
public sealed record ResumoDispositivo(
    Guid Id,
    string Nome,
    string Endereco,
    string? Descricao,
    bool Ativo,
    StatusAtivo Status,
    int TentativasSemResposta,
    int LimiteTentativas,
    int IntervaloSegundos,
    DateTime? UltimaVerificacaoEm,
    int? UltimaLatenciaMs,
    IReadOnlyList<ResumoServico> Servicos)
{
    public int ServicosIndisponiveis => Servicos.Count(s => s.Ativo && s.Status == StatusAtivo.Offline);
    public bool EstaVerificando => Ativo && Status != StatusAtivo.Offline && TentativasSemResposta > 0;
}

public sealed record ResumoServico(Guid Id, string Nome, int Porta, bool Ativo, StatusAtivo Status, int TentativasSemResposta)
{
    public bool EstaVerificando => Ativo && Status != StatusAtivo.Offline && TentativasSemResposta > 0;
}

/// <summary>Ponto da série de latência. <see cref="LatenciaMs"/> nulo indica ausência de resposta.</summary>
public sealed record PontoLatencia(DateTime Instante, int? LatenciaMs);

/// <summary>Índice de disponibilidade de um ativo em um período.</summary>
public sealed record IndiceDisponibilidade(
    decimal PercentualDisponivel,
    int TotalVerificacoes,
    int VerificacoesComSucesso,
    TimeSpan TempoIndisponivel,
    int QuantidadeQuedas,
    double? LatenciaMediaMs);

public enum TipoOcorrencia
{
    Queda,
    Restabelecimento,
    FalhaDeVerificacao,
    VerificacaoComSucesso
}

public sealed record FiltroHistorico(
    Guid? DispositivoId,
    DateTime De,
    DateTime Ate,
    IReadOnlyCollection<TipoOcorrencia>? Tipos = null,
    int Limite = 500);

/// <summary>Linha do histórico: um alerta ou uma verificação.</summary>
public sealed record EventoHistorico(
    Guid Id,
    DateTime Instante,
    TipoOcorrencia Tipo,
    Guid DispositivoId,
    string NomeDispositivo,
    string? NomeServico,
    int? Porta,
    string? Descricao,
    int? LatenciaMs,
    DateTime? EncerradoEm)
{
    public TimeSpan? Duracao => Tipo == TipoOcorrencia.Queda ? (EncerradoEm ?? DateTime.UtcNow) - Instante : null;
    public bool EmAberto => Tipo == TipoOcorrencia.Queda && EncerradoEm is null;
}
