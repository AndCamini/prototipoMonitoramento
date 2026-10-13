using Monitoramento.Dominio.Entidades;
using Monitoramento.Dominio.Enumeracoes;

namespace Monitoramento.Testes;

public class AlertaTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Queda_nasce_aberta_e_encerrar_calcula_a_duracao()
    {
        var alerta = new Alerta(Guid.NewGuid(), null, TipoEvento.Queda, T0, "caiu");
        Assert.True(alerta.EstaAberto());
        Assert.Null(alerta.Duracao());

        alerta.Encerrar(T0.AddMinutes(5));
        Assert.False(alerta.EstaAberto());
        Assert.Equal(TimeSpan.FromMinutes(5), alerta.Duracao());
    }

    [Fact]
    public void Encerramento_nunca_e_anterior_ao_inicio()
    {
        var alerta = new Alerta(Guid.NewGuid(), null, TipoEvento.Queda, T0, null);
        alerta.Encerrar(T0.AddMinutes(-1));
        Assert.Equal(T0, alerta.EncerradoEm);
    }

    [Fact]
    public void Restabelecimento_e_um_evento_pontual()
    {
        var alerta = new Alerta(Guid.NewGuid(), null, TipoEvento.Restabelecimento, T0, "voltou");
        Assert.False(alerta.EstaAberto());
        Assert.Equal(TimeSpan.Zero, alerta.Duracao());
    }

    [Fact]
    public void Descricao_e_limitada_a_300_caracteres()
    {
        var alerta = new Alerta(Guid.NewGuid(), null, TipoEvento.Queda, T0, new string('x', 400));
        alerta.AcrescentarDescricao("complemento");
        Assert.Equal(Alerta.TamanhoMaximoDescricao, alerta.Descricao!.Length);
    }
}
