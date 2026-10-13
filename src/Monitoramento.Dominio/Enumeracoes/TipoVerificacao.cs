namespace Monitoramento.Dominio.Enumeracoes;

/// <summary>Protocolo utilizado na aferição (coluna <c>verificacao.tipo</c>).</summary>
public enum TipoVerificacao : short
{
    Icmp = 0,
    Tcp = 1
}
