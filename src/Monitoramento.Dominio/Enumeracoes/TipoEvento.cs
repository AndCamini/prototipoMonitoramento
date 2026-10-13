namespace Monitoramento.Dominio.Enumeracoes;

/// <summary>Natureza de um alerta (coluna <c>alerta.tipo</c>).</summary>
public enum TipoEvento : short
{
    Queda = 0,
    Restabelecimento = 1
}
