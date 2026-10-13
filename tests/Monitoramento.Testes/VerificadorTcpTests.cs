using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Monitoramento.Servico;
using Monitoramento.Servico.Verificadores;

namespace Monitoramento.Testes;

public class VerificadorTcpTests
{
    private static VerificadorTcp Criar(int timeoutMs = 1000) =>
        new(Options.Create(new MonitoramentoOptions { TimeoutTcpMs = timeoutMs }), TimeProvider.System);

    [Fact]
    public async Task Conexao_aceita_retorna_sucesso_com_latencia()
    {
        var ouvinte = new TcpListener(IPAddress.Loopback, 0);
        ouvinte.Start();
        try
        {
            var porta = ((IPEndPoint)ouvinte.LocalEndpoint).Port;
            var r = await Criar().VerificarAsync(new AlvoVerificacao("127.0.0.1", porta));
            Assert.True(r.Sucesso);
            Assert.NotNull(r.LatenciaMs);
            Assert.Null(r.MensagemErro);
        }
        finally
        {
            ouvinte.Stop();
        }
    }

    [Fact]
    public async Task Porta_fechada_retorna_conexao_recusada()
    {
        // Reserva uma porta livre e a libera em seguida.
        var ouvinte = new TcpListener(IPAddress.Loopback, 0);
        ouvinte.Start();
        var porta = ((IPEndPoint)ouvinte.LocalEndpoint).Port;
        ouvinte.Stop();

        var r = await Criar().VerificarAsync(new AlvoVerificacao("127.0.0.1", porta));
        Assert.False(r.Sucesso);
        Assert.Null(r.LatenciaMs);
        Assert.Contains("recusada", r.MensagemErro);
    }

    [Fact]
    public async Task Alvo_sem_porta_e_invalido() =>
        await Assert.ThrowsAsync<ArgumentException>(() => Criar().VerificarAsync(new AlvoVerificacao("127.0.0.1")));
}
