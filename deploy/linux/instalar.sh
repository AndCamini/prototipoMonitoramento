#!/usr/bin/env bash
# Publica a aplicação (autocontida, sem exigir o .NET instalado) e a registra no systemd.
# Uso: sudo ./deploy/linux/instalar.sh
set -euo pipefail

DESTINO=/opt/monitoramento
RAIZ="$(cd "$(dirname "$0")/../.." && pwd)"
ARQUITETURA="linux-x64"
[[ "$(uname -m)" == "aarch64" ]] && ARQUITETURA="linux-arm64"

if [[ $EUID -ne 0 ]]; then
    echo "Execute como root (sudo)." >&2
    exit 1
fi

echo ">> Publicando para $ARQUITETURA em $DESTINO"
dotnet publish "$RAIZ/src/Monitoramento.Web" -c Release -r "$ARQUITETURA" --self-contained true -o "$DESTINO"

id -u monitoramento &>/dev/null || useradd --system --no-create-home --shell /usr/sbin/nologin monitoramento
chown -R monitoramento:monitoramento "$DESTINO"

install -m 644 "$RAIZ/deploy/linux/monitoramento.service" /etc/systemd/system/monitoramento.service
systemctl daemon-reload
systemctl enable --now monitoramento.service

echo ">> Serviço instalado. Situação: systemctl status monitoramento"
echo ">> Logs: journalctl -u monitoramento -f"
echo ">> Interface: http://localhost:5080"
