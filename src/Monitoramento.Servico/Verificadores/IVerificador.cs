using Monitoramento.Dominio;
using Monitoramento.Dominio.Enumeracoes;

namespace Monitoramento.Servico.Verificadores;

/// <summary>
/// Contrato único das aferições. Principal ponto de extensão do sistema: uma verificação
/// adicional (por exemplo, HTTP) entra como nova implementação, sem alterar o ciclo principal.
/// </summary>
public interface IVerificador
{
    TipoVerificacao Tipo { get; }

    Task<ResultadoVerificacao> VerificarAsync(AlvoVerificacao alvo, CancellationToken ct = default);
}

/// <summary>Endereço (IP ou nome de host) e, nas verificações TCP, a porta a ser testada.</summary>
public sealed record AlvoVerificacao(string Endereco, int? Porta = null);
