<#
.SYNOPSIS
  Publica a aplicação e a registra como Windows Service (RNF03).
.DESCRIPTION
  O serviço inicia automaticamente com o Windows e é reiniciado pelo próprio sistema em caso
  de encerramento inesperado. Execute em um PowerShell aberto como Administrador:
    powershell -ExecutionPolicy Bypass -File .\deploy\windows\instalar-servico.ps1
#>
param(
    [string]$Destino = "C:\Monitoramento",
    [string]$NomeServico = "MonitoramentoDisponibilidade"
)
$ErrorActionPreference = "Stop"
$raiz = Resolve-Path (Join-Path $PSScriptRoot "..\..")

if (Get-Service -Name $NomeServico -ErrorAction SilentlyContinue) {
    Write-Host ">> Parando serviço existente"
    Stop-Service -Name $NomeServico -Force
}

Write-Host ">> Publicando em $Destino"
dotnet publish "$raiz\src\Monitoramento.Web" -c Release -r win-x64 --self-contained true -o $Destino
if ($LASTEXITCODE -ne 0) { throw "Falha na publicação." }

$exe = Join-Path $Destino "Monitoramento.Web.exe"
if (-not (Get-Service -Name $NomeServico -ErrorAction SilentlyContinue)) {
    Write-Host ">> Registrando o serviço"
    sc.exe create $NomeServico binPath= "`"$exe`"" start= delayed-auto DisplayName= "Monitoramento de Disponibilidade de Rede" | Out-Null
    sc.exe description $NomeServico "Verificação agentless (ICMP e TCP) de dispositivos e serviços da rede." | Out-Null
}

# Reinicia automaticamente após falhas (10 s), zerando a contagem diariamente.
sc.exe failure $NomeServico reset= 86400 actions= restart/10000/restart/10000/restart/10000 | Out-Null

Start-Service -Name $NomeServico
Write-Host ">> Serviço instalado e iniciado. Interface: http://localhost:5080"
Write-Host ">> Logs: Visualizador de Eventos > Logs do Windows > Aplicativo"
