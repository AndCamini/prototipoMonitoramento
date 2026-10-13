using Monitoramento.Dominio;
using Monitoramento.Dominio.Entidades;
using Monitoramento.Dominio.Enumeracoes;

namespace Monitoramento.Testes;

/// <summary>Máquina de estados da Figura 6 aplicada ao dispositivo.</summary>
public class DispositivoTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
    private static ResultadoVerificacao Ok(int segundos = 0) => ResultadoVerificacao.Ok(5, T0.AddSeconds(segundos));
    private static ResultadoVerificacao Falha(int segundos = 0) => ResultadoVerificacao.Falha("sem resposta", T0.AddSeconds(segundos));

    [Fact]
    public void Novo_dispositivo_inicia_desconhecido_e_ativo()
    {
        var d = new Dispositivo("Servidor", "192.168.0.10");
        Assert.Equal(StatusAtivo.Desconhecido, d.Status);
        Assert.True(d.Ativo);
        Assert.Equal(0, d.TentativasSemResposta);
        Assert.Null(d.UltimaVerificacaoEm);
    }

    [Fact]
    public void Primeira_verificacao_com_sucesso_leva_a_online()
    {
        var d = new Dispositivo("Servidor", "192.168.0.10");
        Assert.True(d.AplicarResultado(Ok()));
        Assert.Equal(StatusAtivo.Online, d.Status);
        Assert.Equal(StatusAtivo.Desconhecido, d.StatusAnterior);
        Assert.Equal(T0, d.UltimaVerificacaoEm);
    }

    [Fact]
    public void Falhas_abaixo_do_limite_mantem_estado_verificando_sem_transicao()
    {
        var d = new Dispositivo("Servidor", "192.168.0.10", limiteTentativas: 3);
        d.AplicarResultado(Ok());

        Assert.False(d.AplicarResultado(Falha(60)));
        Assert.False(d.AplicarResultado(Falha(120)));
        Assert.Equal(StatusAtivo.Online, d.Status);
        Assert.Equal(2, d.TentativasSemResposta);
        Assert.True(d.EstaVerificando);
    }

    [Fact]
    public void Sucesso_durante_verificando_zera_contador_sem_transicao()
    {
        var d = new Dispositivo("Servidor", "192.168.0.10", limiteTentativas: 3);
        d.AplicarResultado(Ok());
        d.AplicarResultado(Falha(60));

        Assert.False(d.AplicarResultado(Ok(120)));
        Assert.Equal(0, d.TentativasSemResposta);
        Assert.False(d.EstaVerificando);
    }

    [Fact]
    public void Atingir_o_limite_de_tentativas_leva_a_offline()
    {
        var d = new Dispositivo("Servidor", "192.168.0.10", limiteTentativas: 3);
        d.AplicarResultado(Ok());
        d.AplicarResultado(Falha(60));
        d.AplicarResultado(Falha(120));

        Assert.True(d.AplicarResultado(Falha(180)));
        Assert.Equal(StatusAtivo.Offline, d.Status);
        Assert.Equal(StatusAtivo.Online, d.StatusAnterior);
        Assert.False(d.EstaVerificando);
    }

    [Fact]
    public void Novas_falhas_em_offline_nao_geram_transicao()
    {
        var d = new Dispositivo("Servidor", "192.168.0.10", limiteTentativas: 1);
        d.AplicarResultado(Falha());
        Assert.Equal(StatusAtivo.Offline, d.Status);

        Assert.False(d.AplicarResultado(Falha(60)));
        Assert.Equal(StatusAtivo.Offline, d.Status);
    }

    [Fact]
    public void Retorno_de_offline_leva_a_online_com_transicao()
    {
        var d = new Dispositivo("Servidor", "192.168.0.10", limiteTentativas: 1);
        d.AplicarResultado(Falha());

        Assert.True(d.AplicarResultado(Ok(60)));
        Assert.Equal(StatusAtivo.Online, d.Status);
        Assert.Equal(StatusAtivo.Offline, d.StatusAnterior);
    }

    [Fact]
    public void Desconhecido_vai_direto_a_offline_quando_nunca_responde()
    {
        var d = new Dispositivo("Servidor", "192.168.0.10", limiteTentativas: 2);
        Assert.False(d.AplicarResultado(Falha()));
        Assert.True(d.AplicarResultado(Falha(60)));
        Assert.Equal(StatusAtivo.Offline, d.Status);
    }

    [Fact]
    public void DeveVerificar_respeita_o_intervalo_configurado()
    {
        var d = new Dispositivo("Servidor", "192.168.0.10", intervaloSegundos: 60);
        Assert.True(d.DeveVerificar(T0));

        d.AplicarResultado(Ok());
        Assert.False(d.DeveVerificar(T0.AddSeconds(58)));
        Assert.True(d.DeveVerificar(T0.AddSeconds(59))); // tolerância de agendamento
        Assert.True(d.DeveVerificar(T0.AddSeconds(60)));
    }

    [Fact]
    public void Dispositivo_desativado_nao_e_verificado_e_volta_ao_estado_inicial()
    {
        var d = new Dispositivo("Servidor", "192.168.0.10", limiteTentativas: 1);
        d.AplicarResultado(Falha());

        d.Desativar();
        Assert.False(d.DeveVerificar(T0.AddHours(1)));
        Assert.Equal(StatusAtivo.Desconhecido, d.Status);

        d.Reativar();
        Assert.True(d.DeveVerificar(T0));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(3601)]
    public void Intervalo_fora_da_faixa_e_rejeitado(int intervalo) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Dispositivo("X", "10.0.0.1", intervaloSegundos: intervalo));

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void Limite_de_tentativas_fora_da_faixa_e_rejeitado(int limite) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Dispositivo("X", "10.0.0.1", limiteTentativas: limite));

    [Fact]
    public void Porta_duplicada_no_mesmo_dispositivo_e_rejeitada()
    {
        var d = new Dispositivo("Servidor", "192.168.0.10");
        d.AdicionarServico("SMB", 445);
        Assert.Throws<InvalidOperationException>(() => d.AdicionarServico("Outro", 445));
    }

    [Fact]
    public void Servico_tcp_usa_o_limite_do_dispositivo_e_e_independente_do_host()
    {
        var d = new Dispositivo("Servidor", "192.168.0.10", limiteTentativas: 2);
        var smb = d.AdicionarServico("SMB", 445);
        d.AplicarResultado(Ok());

        Assert.False(smb.AplicarResultado(Falha()));
        Assert.True(smb.AplicarResultado(Falha(60)));
        Assert.Equal(StatusAtivo.Offline, smb.Status);
        Assert.Equal(StatusAtivo.Online, d.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Porta_fora_da_faixa_e_rejeitada(int porta) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Dispositivo("X", "10.0.0.1").AdicionarServico("S", porta));
}
