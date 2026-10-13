# Protótipo de Monitoramento de Disponibilidade de Ativos de Rede

Ferramenta **agentless** de verificação ativa de disponibilidade para pequenas e médias
empresas sem equipe de TI dedicada. A partir de uma única máquina da rede interna, o
sistema envia requisições **ICMP** (alcançabilidade e latência) e tentativas de conexão
**TCP** (serviços nas portas cadastradas). Com isso, mantém um painel em tempo real, gera
alertas nas transições de estado e guarda um histórico para gráficos de latência e índices
de disponibilidade.

A implementação segue o Capítulo 4 (*Desenvolvimento do Sistema*) do TCC: requisitos
RF01–RF15 e RNF01–RNF11, arquitetura em quatro componentes, diagramas UML e modelo de dados.

## Arquitetura

```
src/
├── Monitoramento.Dominio        Núcleo de Domínio (biblioteca sem dependências externas)
│                                 Dispositivo, ServicoTcp, Verificacao, Alerta, enumerações,
│                                 máquina de estados e contrato INotificadorDeEstado
├── Monitoramento.Persistencia   Camada de Persistência (EF Core + Npgsql)
│                                 MonitoramentoDbContext, migrações, IRepositorioDispositivo,
│                                 IRepositorioMonitoramento, IConsultaPainel e DTOs
├── Monitoramento.Servico        Serviço de Monitoramento (Worker Service)
│                                 MonitoramentoWorker, IVerificador, VerificadorIcmp,
│                                 VerificadorTcp, AvaliadorDeEstado, MonitoramentoOptions
└── Monitoramento.Web            Interface Web (Blazor Web App, interativo no servidor)
                                  e ponto de entrada do executável único
tests/
└── Monitoramento.Testes         Testes automatizados (xUnit)
deploy/                          Registro como unidade do systemd e como Windows Service
docs/esquema.sql                 Script SQL equivalente às migrações (Listing 1 do TCC)
```

Os quatro componentes são compilados como projetos distintos e publicados como **um único
executável**, registrado no sistema operacional (Seção 4.3). A interface web assina o evento
publicado por `INotificadorDeEstado`, de modo que o painel se atualiza no momento em que uma
mudança de estado é detectada, sem consultas periódicas ao banco.

### Ciclo de verificação (Figuras 3 e 5)

1. A cada pulso (5 s), o `MonitoramentoWorker` lê os dispositivos ativos e seleciona aqueles
   cujo intervalo próprio já transcorreu (`Dispositivo.DeveVerificar`).
2. Para cada dispositivo, **em paralelo** (até `MaxVerificacoesSimultaneas`), envia um ping.
3. Se houver resposta, testa em paralelo cada porta TCP cadastrada.
4. Aplica os resultados às entidades (`AplicarResultado`), que atualizam o contador de falhas
   e o status e informam se houve transição.
5. O `AvaliadorDeEstado` decide se a transição gera alerta de **queda** ou de **restabelecimento**.
6. Grava sempre as verificações. Grava o alerta apenas quando há transição; o restabelecimento
   encerra o alerta de queda em aberto e registra a duração da indisponibilidade.
7. Notifica o painel Blazor.

### Máquina de estados (Figura 6)

`Desconhecido → Online` na primeira resposta. Uma falha leva ao estado transitório
**Verificando**, que não é persistido como valor próprio: é derivado de
`TentativasSemResposta > 0`, mantendo a coluna `status` restrita a 0, 1 e 2, como no
dicionário de dados. Ao atingir `LimiteTentativas`, o ativo passa a **Offline** e o alerta de
queda é registrado. O retorno a **Online** encerra o alerta. Cada serviço TCP tem a mesma
máquina de estados, independente da do dispositivo.

## Rastreabilidade dos requisitos

| Requisito | Onde está implementado |
|---|---|
| RF01 Cadastro de dispositivos | `CadastroDispositivoPage.razor`, `FormularioDispositivo`, `IRepositorioDispositivo.AdicionarAsync` |
| RF02 Editar e excluir | `CadastroDispositivoPage.razor` (edição), `DispositivosPage.razor` (exclusão em cascata) |
| RF03 Ativar e desativar sem remover | `Dispositivo.Desativar/Reativar`, chave na lista de dispositivos |
| RF04 Serviços TCP por dispositivo | `Dispositivo.AdicionarServico`, seção “Serviços TCP” do cadastro |
| RF05 Intervalo padrão e individual | `MonitoramentoOptions.IntervaloPadraoSegundos`, campo no cadastro, “Aplicar a todos” em Configurações |
| RF06 Verificação ICMP periódica | `VerificadorIcmp`, `MonitoramentoWorker` |
| RF07 Conexão TCP aceita, recusada ou expirada | `VerificadorTcp` (mensagens distintas para cada desfecho) |
| RF08 Gravação de cada verificação | `Verificacao`, `IRepositorioMonitoramento.SalvarVerificacoesAsync` |
| RF09 Estado atual de dispositivo e serviço | `AtivoMonitorado.AplicarResultado`, `IRepositorioDispositivo.AtualizarEstadoAsync` |
| RF10 Tolerância a falhas consecutivas | `LimiteTentativas` + `TentativasSemResposta` (estado “Verificando”) |
| RF11 Alerta na queda e no restabelecimento | `AvaliadorDeEstado`, `Alerta.Encerrar/Duracao` |
| RF12 Painel atualizado sem recarregar | `PainelPage.razor` assinando `INotificadorDeEstado` |
| RF13 Gráfico de latência por período | `GraficosPage.razor`, `GraficoLatencia.razor` (SVG), `IConsultaPainel.ObterLatenciaAsync` |
| RF14 Percentual de disponibilidade | `IConsultaPainel.ObterDisponibilidadeAsync` (dispositivo e serviço) |
| RF15 Histórico com filtros | `HistoricoPage.razor`, `IConsultaPainel.ObterHistoricoAsync` (dispositivo, período e tipo) |
| RNF01 Multiplataforma (.NET 10) | Todos os projetos usam `net10.0`; scripts para Linux e Windows |
| RNF02 Agentless | Somente ICMP e TCP a partir da máquina central |
| RNF03 Serviço do SO com reinício automático | `AddWindowsService`/`AddSystemd` em `Program.cs`, `deploy/` |
| RNF04 Execução assíncrona e paralela | `Parallel.ForEachAsync` + `Task.WhenAll` no `MonitoramentoWorker` |
| RNF05 Consumo compatível com hardware de escritório | GC de estação de trabalho, gráfico em SVG sem bibliotecas JS (ver medição abaixo) |
| RNF06 Única dependência externa: PostgreSQL | `docker-compose.yml`; publicação autocontida dispensa instalar o .NET |
| RNF07 Esquema por migrações do EF Core | `Migracoes/`, aplicadas automaticamente na inicialização (`AplicarMigracoesAsync`) |
| RNF08 Tempo real e linguagem acessível | Rótulos “Disponível / Verificando / Indisponível” com ícone, mensagens em português simples |
| RNF09 Falha temporária não encerra o serviço | `try/catch` por ciclo e por dispositivo no worker; novas tentativas de conexão ao banco na inicialização |
| RNF10 Sem licença nem limite de dispositivos | Código aberto e nenhum limite artificial |
| RNF11 Detecção em até 2 ciclos + tolerância | Agendamento por pulso de 5 s com `ToleranciaAgendamento` de 1 s |

## Requisitos para executar

- [.NET SDK 10](https://dotnet.microsoft.com/download) (apenas para compilar ou publicar)
- PostgreSQL 16 (ou superior), por instalador nativo ou por contêiner

## Execução em desenvolvimento

```bash
# 1. Suba o PostgreSQL (ou use uma instalação existente)
docker compose up -d

# 2. Execute a aplicação. O esquema do banco é criado automaticamente.
dotnet run --project src/Monitoramento.Web

# 3. Acesse http://localhost:5080
```

A cadeia de conexão fica em `src/Monitoramento.Web/appsettings.json`
(`ConnectionStrings:Monitoramento`). Também pode ser informada pela variável de ambiente
`ConnectionStrings__Monitoramento`.

## Testes

```bash
dotnet test
```

São 36 testes. Eles cobrem a máquina de estados (incluindo a tolerância do RF10), a geração
e o encerramento de alertas, o verificador TCP (conexão aceita e recusada), a redução da série
do gráfico e o ciclo completo do worker com repositórios simulados: paralelismo, intervalo
por dispositivo e isolamento de falhas (RNF09).

## Implantação

Para que reinstalações não sobrescrevam suas configurações, coloque-as em
`appsettings.Production.json` na pasta de instalação, e não no `appsettings.json`.

### Linux (systemd)

```bash
sudo ./deploy/linux/instalar.sh          # publica em /opt/monitoramento e registra o serviço
systemctl status monitoramento
journalctl -u monitoramento -f           # alertas de queda aparecem também no log
```

A unidade concede `CAP_NET_RAW` para que o ping funcione sem executar como root.

### Windows (Windows Service)

Em um PowerShell aberto como Administrador:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy\windows\instalar-servico.ps1
```

O serviço `MonitoramentoDisponibilidade` inicia com o Windows (início automático atrasado) e
é reiniciado em caso de falha.

### Acesso pela rede

Por padrão, a interface escuta apenas em `http://localhost:5080`, pois o protótipo não tem
autenticação. Para acessá-la de outras máquinas da rede, altere `Urls` (por exemplo,
`http://0.0.0.0:5080`) e restrinja o acesso no firewall.

## Parâmetros (`appsettings.json`, seção `Monitoramento`)

| Parâmetro | Padrão | Descrição |
|---|---|---|
| `IntervaloPadraoSegundos` | 60 | Intervalo sugerido no cadastro de novos dispositivos |
| `LimiteTentativasPadrao` | 3 | Falhas seguidas antes de declarar indisponibilidade |
| `TimeoutIcmpMs` | 1000 | Tempo máximo de espera pelo Echo Reply |
| `TimeoutTcpMs` | 2000 | Tempo máximo para estabelecer a conexão TCP |
| `MaxVerificacoesSimultaneas` | 20 | Dispositivos verificados em paralelo |
| `PulsoSegundos` | 5 | Frequência com que o worker procura dispositivos a verificar |

## Medição preliminar de consumo (RNF05)

Ambiente: Linux, contêiner com 4 núcleos, build Release, 20 dispositivos com intervalo de
10 s, cada um com uma porta TCP. Valores médios em 60 s:

- **CPU:** 1,25% do total (234 verificações por minuto; o esperado eram 240).
- **Memória:** cerca de 60 MB de memória privada (anônima). O RSS total foi de 189 MB,
  porque inclui cerca de 86 MB de bibliotecas do runtime .NET mapeadas a partir de arquivo,
  que são compartilháveis, e cerca de 43 MB do mapeamento W^X do JIT.

Para a validação formal, meça no hardware-alvo, por exemplo com “Working Set - Privado” no
Gerenciador de Tarefas ou `RssAnon` em `/proc/<pid>/status`.

## Observações

- Há equipamentos que bloqueiam ICMP, como o Windows com o firewall padrão. Nesse caso, o
  dispositivo aparece como indisponível. O cadastro avisa quando o ping de teste falha e pede
  confirmação antes de salvar. Como define o diagrama de atividades, os serviços TCP só são
  verificados quando o host responde ao ping.
- Todas as datas são gravadas em UTC (`timestamptz`) e exibidas no fuso horário da máquina
  servidora.
