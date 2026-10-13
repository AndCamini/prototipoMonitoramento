<#
.SYNOPSIS
  Remove o Windows Service do monitoramento (os dados do PostgreSQL são mantidos).
#>
param([string]$NomeServico = "MonitoramentoDisponibilidade")
$ErrorActionPreference = "Stop"
if (Get-Service -Name $NomeServico -ErrorAction SilentlyContinue) {
    Stop-Service -Name $NomeServico -Force
    sc.exe delete $NomeServico | Out-Null
    Write-Host "Serviço removido."
} else {
    Write-Host "Serviço não encontrado."
}
