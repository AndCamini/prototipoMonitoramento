namespace Monitoramento.Web.Suporte;

/// <summary>Período selecionado pelo usuário nos gráficos e no histórico (valores em UTC).</summary>
public sealed record Periodo(DateTime De, DateTime Ate)
{
    public static readonly IReadOnlyList<(string Chave, string Rotulo, TimeSpan Duracao)> Predefinidos =
    [
        ("1h", "Última hora", TimeSpan.FromHours(1)),
        ("6h", "Últimas 6 horas", TimeSpan.FromHours(6)),
        ("24h", "Últimas 24 horas", TimeSpan.FromHours(24)),
        ("7d", "Últimos 7 dias", TimeSpan.FromDays(7)),
        ("30d", "Últimos 30 dias", TimeSpan.FromDays(30))
    ];

    public static Periodo Ultimos(TimeSpan duracao)
    {
        var agora = DateTime.UtcNow;
        return new Periodo(agora - duracao, agora);
    }

    public static Periodo DaChave(string chave) =>
        Ultimos(Predefinidos.FirstOrDefault(p => p.Chave == chave).Duracao is { Ticks: > 0 } d ? d : TimeSpan.FromHours(24));
}
