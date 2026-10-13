using Monitoramento.Persistencia.Consultas;
using Monitoramento.Persistencia.Repositorios;

namespace Monitoramento.Testes;

public class ConsultaPainelTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Serie_pequena_nao_e_reduzida()
    {
        var pontos = Enumerable.Range(0, 10).Select(i => new PontoLatencia(T0.AddMinutes(i), i)).ToList();
        Assert.Same(pontos, ConsultaPainel.Reduzir(pontos, T0, T0.AddMinutes(10), 100));
    }

    [Fact]
    public void Serie_grande_e_agrupada_pela_media_e_falhas_viram_nulo()
    {
        // 1000 pontos em 1000 minutos; os 100 primeiros sem resposta.
        var pontos = Enumerable.Range(0, 1000)
            .Select(i => new PontoLatencia(T0.AddMinutes(i), i < 100 ? null : 10))
            .ToList();

        var reduzida = ConsultaPainel.Reduzir(pontos, T0, T0.AddMinutes(1000), 100);

        Assert.True(reduzida.Count <= 101);
        Assert.Null(reduzida[0].LatenciaMs);
        Assert.Equal(10, reduzida[^1].LatenciaMs);
    }
}
