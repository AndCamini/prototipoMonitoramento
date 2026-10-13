using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Monitoramento.Dominio;
using Monitoramento.Dominio.Entidades;
using Monitoramento.Dominio.Enumeracoes;
using Monitoramento.Persistencia.Contratos;
using Monitoramento.Servico;
using Monitoramento.Servico.Verificadores;

namespace Monitoramento.Testes;

/// <summary>Ciclo de verificação (Figuras 3 e 5) com repositórios e verificadores simulados.</summary>
public class MonitoramentoWorkerTests
{
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly RepositorioDispositivoFalso _dispositivos = new();
    private readonly RepositorioMonitoramentoFalso _monitoramento = new();
    private readonly VerificadorFalso _icmp;
    private readonly VerificadorFalso _tcp;
    private readonly NotificadorDeEstado _notificador = new(NullLogger<NotificadorDeEstado>.Instance);
    private readonly MonitoramentoWorker _worker;

    public MonitoramentoWorkerTests()
    {
        _icmp = new VerificadorFalso(TipoVerificacao.Icmp, _relogio);
        _tcp = new VerificadorFalso(TipoVerificacao.Tcp, _relogio);

        var servicos = new ServiceCollection()
            .AddSingleton<IRepositorioDispositivo>(_dispositivos)
            .AddSingleton<IRepositorioMonitoramento>(_monitoramento)
            .BuildServiceProvider();

        _worker = new MonitoramentoWorker(servicos.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new MonitoramentoOptions()), [_icmp, _tcp], new AvaliadorDeEstado(),
            _notificador, _relogio, NullLogger<MonitoramentoWorker>.Instance);
    }

    private async Task CicloAsync()
    {
        await _worker.ExecutarCicloAsync(CancellationToken.None);
        _relogio.Advance(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Registra_verificacao_icmp_e_tcp_a_cada_ciclo()
    {
        var d = new Dispositivo("Servidor", "10.0.0.1", intervaloSegundos: 10);
        d.AdicionarServico("SMB", 445);
        _dispositivos.Itens.Add(d);

        await CicloAsync();

        Assert.Equal(2, _monitoramento.Verificacoes.Count);
        Assert.Contains(_monitoramento.Verificacoes, v => v.Tipo == TipoVerificacao.Icmp && v.ServicoTcpId is null);
        Assert.Contains(_monitoramento.Verificacoes, v => v.Tipo == TipoVerificacao.Tcp && v.ServicoTcpId is not null);
        Assert.Equal(StatusAtivo.Online, _dispositivos.EstadoGravado[d.Id]);
        Assert.Empty(_monitoramento.Alertas);
    }

    [Fact]
    public async Task Alerta_de_queda_so_apos_o_limite_e_restabelecimento_encerra_o_alerta()
    {
        var d = new Dispositivo("Servidor", "10.0.0.1", intervaloSegundos: 10, limiteTentativas: 3);
        _dispositivos.Itens.Add(d);
        var eventos = new List<EstadoAlteradoCapturado>();
        _notificador.EstadoAlterado += e => { eventos.Add(new(e.TipoEvento)); return Task.CompletedTask; };

        await CicloAsync();                         // Desconhecido -> Online
        _icmp.Responde = false;
        await CicloAsync();                         // falha 1
        await CicloAsync();                         // falha 2
        Assert.Empty(_monitoramento.Alertas);       // RF10: perdas isoladas não geram alerta
        await CicloAsync();                         // falha 3 -> Offline

        var queda = Assert.Single(_monitoramento.Alertas);
        Assert.Equal(TipoEvento.Queda, queda.Tipo);

        await CicloAsync();                         // continua Offline: nenhum alerta novo
        Assert.Single(_monitoramento.Alertas);

        _icmp.Responde = true;
        await CicloAsync();                         // Offline -> Online

        Assert.Equal(2, _monitoramento.Alertas.Count);
        Assert.Equal(TipoEvento.Restabelecimento, _monitoramento.Alertas[1].Tipo);
        Assert.False(queda.EstaAberto());
        Assert.Equal(TimeSpan.FromSeconds(20), queda.Duracao());
        Assert.Contains("Indisponível por", _monitoramento.Alertas[1].Descricao);

        Assert.Equal([null, TipoEvento.Queda, TipoEvento.Restabelecimento], eventos.Select(e => e.Tipo));
        Assert.Equal(6, _monitoramento.Verificacoes.Count); // toda verificação é persistida (RF08)
    }

    [Fact]
    public async Task Servicos_nao_sao_verificados_quando_o_host_nao_responde()
    {
        var d = new Dispositivo("Servidor", "10.0.0.1", intervaloSegundos: 10);
        d.AdicionarServico("SMB", 445);
        _dispositivos.Itens.Add(d);
        _icmp.Responde = false;

        await CicloAsync();

        Assert.Equal(0, _tcp.Chamadas);
        Assert.Single(_monitoramento.Verificacoes);
    }

    [Fact]
    public async Task Respeita_o_intervalo_de_cada_dispositivo()
    {
        var d = new Dispositivo("Servidor", "10.0.0.1", intervaloSegundos: 30);
        _dispositivos.Itens.Add(d);

        await CicloAsync(); // t = 0
        await CicloAsync(); // t = 10 (não verifica)
        await CicloAsync(); // t = 20 (não verifica)
        await CicloAsync(); // t = 30

        Assert.Equal(2, _icmp.Chamadas);
    }

    [Fact]
    public async Task Falha_em_um_dispositivo_nao_impede_a_verificacao_dos_demais()
    {
        var problema = new Dispositivo("Com problema", "10.0.0.1", intervaloSegundos: 10);
        var normal = new Dispositivo("Normal", "10.0.0.2", intervaloSegundos: 10);
        _dispositivos.Itens.AddRange([problema, normal]);
        _monitoramento.FalharPara = problema.Id;

        await CicloAsync();

        Assert.Contains(_monitoramento.Verificacoes, v => v.DispositivoId == normal.Id);
        Assert.True(_dispositivos.EstadoGravado.ContainsKey(normal.Id));
    }

    private sealed record EstadoAlteradoCapturado(TipoEvento? Tipo);

    private sealed class VerificadorFalso(TipoVerificacao tipo, TimeProvider relogio) : IVerificador
    {
        private int _chamadas;
        public bool Responde { get; set; } = true;
        public int Chamadas => _chamadas;
        public TipoVerificacao Tipo => tipo;

        public Task<ResultadoVerificacao> VerificarAsync(AlvoVerificacao alvo, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _chamadas);
            var agora = relogio.GetUtcNow().UtcDateTime;
            return Task.FromResult(Responde ? ResultadoVerificacao.Ok(3, agora) : ResultadoVerificacao.Falha("sem resposta", agora));
        }
    }

    /// <summary>Mantém as mesmas instâncias entre ciclos, simulando o estado persistido.</summary>
    private sealed class RepositorioDispositivoFalso : IRepositorioDispositivo
    {
        public List<Dispositivo> Itens { get; } = [];
        public ConcurrentDictionary<Guid, StatusAtivo> EstadoGravado { get; } = new();

        public Task<List<Dispositivo>> ObterAtivosAsync(CancellationToken ct = default) => Task.FromResult(Itens.Where(d => d.Ativo).ToList());
        public Task AtualizarEstadoAsync(Dispositivo d, CancellationToken ct = default)
        {
            EstadoGravado[d.Id] = d.Status;
            return Task.CompletedTask;
        }

        public Task<List<Dispositivo>> ObterTodosAsync(CancellationToken ct = default) => Task.FromResult(Itens.ToList());
        public Task<Dispositivo?> ObterPorIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Itens.FirstOrDefault(d => d.Id == id));
        public Task<bool> EnderecoEmUsoAsync(string endereco, Guid? ignorarId = null, CancellationToken ct = default) => Task.FromResult(false);
        public Task AdicionarAsync(Dispositivo dispositivo, CancellationToken ct = default) => Task.CompletedTask;
        public Task AtualizarAsync(Dispositivo dispositivo, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoverAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> AplicarIntervaloATodosAsync(int intervaloSegundos, CancellationToken ct = default) => Task.FromResult(0);
    }

    private sealed class RepositorioMonitoramentoFalso : IRepositorioMonitoramento
    {
        private readonly Lock _trava = new();
        public List<Verificacao> Verificacoes { get; } = [];
        public List<Alerta> Alertas { get; } = [];
        public Guid? FalharPara { get; set; }

        public Task SalvarVerificacaoAsync(Verificacao v, CancellationToken ct = default) => SalvarVerificacoesAsync([v], ct);

        public Task SalvarVerificacoesAsync(IEnumerable<Verificacao> verificacoes, CancellationToken ct = default)
        {
            var lista = verificacoes.ToList();
            if (lista.Any(v => v.DispositivoId == FalharPara))
                throw new InvalidOperationException("Banco indisponível (simulado).");
            lock (_trava) Verificacoes.AddRange(lista);
            return Task.CompletedTask;
        }

        public Task SalvarAlertaAsync(Alerta alerta, CancellationToken ct = default)
        {
            lock (_trava) Alertas.Add(alerta);
            return Task.CompletedTask;
        }

        public Task<Alerta?> ObterAlertaAbertoAsync(Guid dispositivoId, Guid? servicoTcpId, CancellationToken ct = default) =>
            Task.FromResult(Alertas.FirstOrDefault(a => a.DispositivoId == dispositivoId && a.ServicoTcpId == servicoTcpId
                                                        && a.Tipo == TipoEvento.Queda && a.EstaAberto()));

        public async Task<Alerta?> EncerrarAlertaAbertoAsync(Guid dispositivoId, Guid? servicoTcpId, DateTime em, CancellationToken ct = default)
        {
            var aberto = await ObterAlertaAbertoAsync(dispositivoId, servicoTcpId, ct);
            aberto?.Encerrar(em);
            return aberto;
        }

        public Task EncerrarAlertasAbertosAsync(Guid dispositivoId, DateTime em, CancellationToken ct = default) => Task.CompletedTask;
        public Task<List<Alerta>> ObterHistoricoAsync(Guid? dispositivoId, DateTime de, DateTime ate, CancellationToken ct = default) => Task.FromResult(Alertas.ToList());
    }
}
