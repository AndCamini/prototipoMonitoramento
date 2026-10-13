using Monitoramento.Dominio;
using Monitoramento.Dominio.Entidades;
using Monitoramento.Dominio.Enumeracoes;

namespace Monitoramento.Servico;

/// <summary>
/// Decide se uma transição de estado deve gerar alerta (RF11): a confirmação de
/// indisponibilidade gera alerta de queda e o retorno a partir de Offline gera o evento de
/// restabelecimento. Transições como Desconhecido → Online não geram alerta.
/// </summary>
public sealed class AvaliadorDeEstado
{
    public Alerta? Avaliar(Dispositivo dispositivo, ResultadoVerificacao resultado)
    {
        if (!HouveTransicao(dispositivo.StatusAnterior, dispositivo.Status)) return null;

        return dispositivo.Status == StatusAtivo.Offline
            ? new Alerta(dispositivo.Id, null, TipoEvento.Queda, resultado.AferidoEm,
                $"{dispositivo.Nome} ({dispositivo.Endereco}) parou de responder ao ping após " +
                $"{dispositivo.TentativasSemResposta} tentativa(s) consecutiva(s). {resultado.MensagemErro}".Trim())
            : new Alerta(dispositivo.Id, null, TipoEvento.Restabelecimento, resultado.AferidoEm,
                $"{dispositivo.Nome} ({dispositivo.Endereco}) voltou a responder" +
                (resultado.LatenciaMs is { } ms ? $" (latência de {ms} ms)." : "."));
    }

    public Alerta? Avaliar(ServicoTcp servico, ResultadoVerificacao resultado)
    {
        if (!HouveTransicao(servico.StatusAnterior, servico.Status)) return null;

        var identificacao = $"Serviço {servico.Nome} (porta {servico.Porta}) em {servico.Dispositivo.Nome}";
        return servico.Status == StatusAtivo.Offline
            ? new Alerta(servico.DispositivoId, servico.Id, TipoEvento.Queda, resultado.AferidoEm,
                $"{identificacao} deixou de aceitar conexões. {resultado.MensagemErro}".Trim())
            : new Alerta(servico.DispositivoId, servico.Id, TipoEvento.Restabelecimento, resultado.AferidoEm,
                $"{identificacao} voltou a aceitar conexões.");
    }

    /// <summary>Somente a entrada e a saída do estado Offline são transições que geram alerta.</summary>
    private static bool HouveTransicao(StatusAtivo anterior, StatusAtivo novo) =>
        anterior != novo && (novo == StatusAtivo.Offline || anterior == StatusAtivo.Offline);
}
