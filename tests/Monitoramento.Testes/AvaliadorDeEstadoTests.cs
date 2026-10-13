using Monitoramento.Dominio;
using Monitoramento.Dominio.Entidades;
using Monitoramento.Dominio.Enumeracoes;
using Monitoramento.Servico;

namespace Monitoramento.Testes;

public class AvaliadorDeEstadoTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private readonly AvaliadorDeEstado _avaliador = new();

    [Fact]
    public void Desconhecido_para_online_nao_gera_alerta()
    {
        var d = new Dispositivo("Servidor", "10.0.0.1");
        var r = ResultadoVerificacao.Ok(3, T0);
        d.AplicarResultado(r);
        Assert.Null(_avaliador.Avaliar(d, r));
    }

    [Fact]
    public void Confirmacao_de_indisponibilidade_gera_alerta_de_queda()
    {
        var d = new Dispositivo("Servidor", "10.0.0.1", limiteTentativas: 1);
        var r = ResultadoVerificacao.Falha("Sem resposta", T0);
        d.AplicarResultado(r);

        var alerta = _avaliador.Avaliar(d, r);
        Assert.NotNull(alerta);
        Assert.Equal(TipoEvento.Queda, alerta.Tipo);
        Assert.Equal(T0, alerta.IniciadoEm);
        Assert.True(alerta.EstaAberto());
        Assert.Null(alerta.ServicoTcpId);
        Assert.Contains("Servidor", alerta.Descricao);
    }

    [Fact]
    public void Retorno_de_offline_gera_restabelecimento()
    {
        var d = new Dispositivo("Servidor", "10.0.0.1", limiteTentativas: 1);
        d.AplicarResultado(ResultadoVerificacao.Falha("x", T0));
        var r = ResultadoVerificacao.Ok(4, T0.AddMinutes(2));
        d.AplicarResultado(r);

        var alerta = _avaliador.Avaliar(d, r);
        Assert.Equal(TipoEvento.Restabelecimento, alerta?.Tipo);
    }

    [Fact]
    public void Queda_de_servico_referencia_dispositivo_e_servico()
    {
        var d = new Dispositivo("Servidor", "10.0.0.1", limiteTentativas: 1);
        var smb = d.AdicionarServico("SMB", 445);
        var r = ResultadoVerificacao.Falha("Conexão recusada", T0);
        smb.AplicarResultado(r);

        var alerta = _avaliador.Avaliar(smb, r);
        Assert.NotNull(alerta);
        Assert.Equal(d.Id, alerta.DispositivoId);
        Assert.Equal(smb.Id, alerta.ServicoTcpId);
        Assert.Equal(TipoEvento.Queda, alerta.Tipo);
    }
}
