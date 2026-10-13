using System.ComponentModel.DataAnnotations;
using System.Net;
using Monitoramento.Dominio.Entidades;

namespace Monitoramento.Web.Suporte;

/// <summary>Dados informados na tela de cadastro de dispositivo (RF01, RF04, RF05).</summary>
public sealed class FormularioDispositivo
{
    [Required(ErrorMessage = "Informe um nome para o dispositivo.")]
    [StringLength(120, ErrorMessage = "O nome deve ter no máximo 120 caracteres.")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o endereço IP ou o nome de host.")]
    [StringLength(255, ErrorMessage = "O endereço deve ter no máximo 255 caracteres.")]
    [CustomValidation(typeof(FormularioDispositivo), nameof(ValidarEndereco))]
    public string Endereco { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "A descrição deve ter no máximo 500 caracteres.")]
    public string? Descricao { get; set; }

    [Range(Dispositivo.IntervaloMinimoSegundos, Dispositivo.IntervaloMaximoSegundos,
        ErrorMessage = "O intervalo deve estar entre 10 e 3600 segundos.")]
    public int IntervaloSegundos { get; set; } = 60;

    [Range(Dispositivo.LimiteTentativasMinimo, Dispositivo.LimiteTentativasMaximo,
        ErrorMessage = "A tolerância deve estar entre 1 e 10 falhas consecutivas.")]
    public int LimiteTentativas { get; set; } = 3;

    public bool Ativo { get; set; } = true;

    public List<FormularioServico> Servicos { get; set; } = [];

    /// <summary>Aceita endereço IPv4/IPv6 ou nome de host válido.</summary>
    public static ValidationResult? ValidarEndereco(string? endereco, ValidationContext contexto)
    {
        var valor = endereco?.Trim();
        if (string.IsNullOrEmpty(valor)) return ValidationResult.Success;
        if (IPAddress.TryParse(valor, out _) || Uri.CheckHostName(valor) == UriHostNameType.Dns)
            return ValidationResult.Success;
        return new ValidationResult("Endereço inválido. Use um IP (ex.: 192.168.0.10) ou um nome de host (ex.: servidor.local).",
            [nameof(Endereco)]);
    }

    /// <summary>Valida a lista de serviços; devolve as mensagens de erro encontradas.</summary>
    public List<string> ValidarServicos()
    {
        var erros = new List<string>();
        foreach (var (servico, i) in Servicos.Select((s, i) => (s, i + 1)))
        {
            if (string.IsNullOrWhiteSpace(servico.Nome)) erros.Add($"Serviço {i}: informe o nome.");
            else if (servico.Nome.Trim().Length > 120) erros.Add($"Serviço {i}: o nome deve ter no máximo 120 caracteres.");
            if (servico.Porta is null or < ServicoTcp.PortaMinima or > ServicoTcp.PortaMaxima)
                erros.Add($"Serviço {i}: a porta deve estar entre 1 e 65535.");
        }
        foreach (var repetida in Servicos.Where(s => s.Porta is not null).GroupBy(s => s.Porta).Where(g => g.Count() > 1))
            erros.Add($"A porta {repetida.Key} foi informada mais de uma vez.");
        return erros;
    }

    public static FormularioDispositivo De(Dispositivo d) => new()
    {
        Nome = d.Nome,
        Endereco = d.Endereco,
        Descricao = d.Descricao,
        IntervaloSegundos = d.IntervaloSegundos,
        LimiteTentativas = d.LimiteTentativas,
        Ativo = d.Ativo,
        Servicos = d.Servicos.OrderBy(s => s.Porta)
            .Select(s => new FormularioServico { Id = s.Id, Nome = s.Nome, Porta = s.Porta, Ativo = s.Ativo })
            .ToList()
    };
}

public sealed class FormularioServico
{
    public Guid? Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public int? Porta { get; set; }
    public bool Ativo { get; set; } = true;
}

/// <summary>Serviços comuns em pequenas e médias empresas, oferecidos como atalho no cadastro.</summary>
public static class ServicosComuns
{
    public static readonly IReadOnlyList<(string Nome, int Porta)> Lista =
    [
        ("HTTP", 80), ("HTTPS", 443), ("Compartilhamento SMB", 445), ("Área de trabalho remota (RDP)", 3389),
        ("SSH", 22), ("DNS", 53), ("SMTP", 25), ("Impressão (IPP)", 631), ("Impressão RAW/JetDirect", 9100),
        ("SQL Server", 1433), ("PostgreSQL", 5432), ("MySQL", 3306), ("FTP", 21)
    ];
}
