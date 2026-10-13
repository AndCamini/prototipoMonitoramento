using Monitoramento.Dominio.Enumeracoes;
using Monitoramento.Persistencia.Consultas;

namespace Monitoramento.Web.Suporte;

/// <summary>Apresentação em linguagem acessível (RNF08) e conversões de fuso horário.</summary>
public static class Formatacao
{
    // O banco armazena UTC (timestamptz); a interface exibe o horário local da máquina.
    public static string DataHora(DateTime? utc) =>
        utc is null ? "—" : DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");

    public static DateTime ParaUtc(DateTime local) =>
        local.Kind == DateTimeKind.Utc ? local : DateTime.SpecifyKind(local, DateTimeKind.Local).ToUniversalTime();

    public static DateTime ParaLocal(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();

    public static string Relativo(DateTime? utc)
    {
        if (utc is null) return "nunca";
        var decorrido = DateTime.UtcNow - utc.Value;
        return decorrido.TotalSeconds < 60 ? $"há {Math.Max(0, (int)decorrido.TotalSeconds)}s"
            : decorrido.TotalMinutes < 60 ? $"há {(int)decorrido.TotalMinutes} min"
            : decorrido.TotalHours < 24 ? $"há {(int)decorrido.TotalHours} h"
            : $"há {(int)decorrido.TotalDays} dia(s)";
    }

    public static string Duracao(TimeSpan? duracao) =>
        duracao is null ? "—" : Servico.MonitoramentoWorker.FormatarDuracao(duracao.Value);

    public static string Latencia(int? ms) => ms is null ? "—" : $"{ms} ms";

    /// <summary>Classe CSS e rótulo de um ativo. O estado "Verificando" é derivado do contador de falhas.</summary>
    public static (string Classe, string Rotulo) Status(bool ativo, StatusAtivo status, int tentativas) =>
        !ativo ? ("desativado", "Desativado")
        : status == StatusAtivo.Offline ? ("offline", "Indisponível")
        : tentativas > 0 ? ("verificando", "Verificando")
        : status == StatusAtivo.Online ? ("online", "Disponível")
        : ("desconhecido", "Aguardando verificação");

    public static string Ocorrencia(TipoOcorrencia tipo) => tipo switch
    {
        TipoOcorrencia.Queda => "Queda",
        TipoOcorrencia.Restabelecimento => "Restabelecimento",
        TipoOcorrencia.FalhaDeVerificacao => "Verificação sem resposta",
        TipoOcorrencia.VerificacaoComSucesso => "Verificação com sucesso",
        _ => tipo.ToString()
    };

    public static string ClasseOcorrencia(TipoOcorrencia tipo) => tipo switch
    {
        TipoOcorrencia.Queda => "offline",
        TipoOcorrencia.Restabelecimento => "online",
        TipoOcorrencia.FalhaDeVerificacao => "verificando",
        _ => "desconhecido"
    };
}
