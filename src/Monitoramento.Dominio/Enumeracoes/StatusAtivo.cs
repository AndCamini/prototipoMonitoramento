namespace Monitoramento.Dominio.Enumeracoes;

/// <summary>
/// Estado de disponibilidade de um ativo monitorado (coluna <c>status</c>: 0, 1 ou 2).
/// O estado transitório "Verificando" da máquina de estados (Figura 6) não é persistido
/// como valor próprio: ele é derivado do contador <c>TentativasSemResposta</c>.
/// </summary>
public enum StatusAtivo : short
{
    Desconhecido = 0,
    Online = 1,
    Offline = 2
}
