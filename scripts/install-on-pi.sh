#!/bin/sh
set -eu

if [ "$(id -u)" -ne 0 ]; then
    echo "Run this installer with sudo."
    exit 1
fi

NCR_DEVICE="${NCR_DEVICE:-/dev/ttyUSB0}"
SOURCE_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
INSTALL_DIR=/opt/ncr7198-bridge
ENV_FILE=/etc/ncr7198-bridge.env
SERVICE_FILE=/etc/systemd/system/ncr7198-bridge.service

if [ ! -c "$NCR_DEVICE" ]; then
    echo "Printer device $NCR_DEVICE was not found. Connect the printer and verify /dev/ttyUSB0."
    exit 1
fi

if [ ! -x "$SOURCE_DIR/Ncr7198.PiBridge" ]; then
    echo "Ncr7198.PiBridge executable is missing. Run Publish-Pi.ps1 and copy the complete publish directory."
    exit 1
fi

if ! id ncrprint >/dev/null 2>&1; then
    useradd --system --no-create-home --home-dir "$INSTALL_DIR" --shell /usr/sbin/nologin ncrprint
fi
usermod -a -G dialout ncrprint

install -d -o root -g root -m 0755 "$INSTALL_DIR"
install -o root -g root -m 0755 "$SOURCE_DIR/Ncr7198.PiBridge" "$INSTALL_DIR/Ncr7198.PiBridge"
install -o root -g root -m 0755 "$SOURCE_DIR/start-bridge.sh" "$INSTALL_DIR/start-bridge.sh"

find "$SOURCE_DIR" -maxdepth 1 -type f ! -name 'Ncr7198.PiBridge' ! -name 'install-on-pi.sh' ! -name 'start-bridge.sh' \
    -exec install -o root -g root -m 0644 '{}' "$INSTALL_DIR/" ';'
find "$SOURCE_DIR" -mindepth 1 -maxdepth 1 -type d \
    -exec cp -a '{}' "$INSTALL_DIR/" ';'
chown -R root:root "$INSTALL_DIR"
chmod -R a+rX "$INSTALL_DIR"

{
    echo "Bridge__DevicePath=$NCR_DEVICE"
    echo "Bridge__Transport=Device"
    echo "Bridge__ListenUrl=http://0.0.0.0:9719"
} > "$ENV_FILE"
chmod 0644 "$ENV_FILE"

cat > "$SERVICE_FILE" <<'UNIT'
[Unit]
Description=NCR 7198 Raspberry Pi Print Bridge
After=network-online.target
Wants=network-online.target

[Service]
Type=simple
User=ncrprint
Group=dialout
EnvironmentFile=/etc/ncr7198-bridge.env
WorkingDirectory=/opt/ncr7198-bridge
ExecStart=/opt/ncr7198-bridge/start-bridge.sh
Restart=on-failure
RestartSec=3
NoNewPrivileges=true
PrivateTmp=true
ProtectHome=true

[Install]
WantedBy=multi-user.target
UNIT

systemctl daemon-reload
systemctl enable --now ncr7198-bridge.service

PI_IP="$(hostname -I | awk '{print $1}')"
echo
echo "NCR 7198 bridge installed."
echo "Web UI:  http://$PI_IP:9719/"
echo "Health:  http://$PI_IP:9719/health"
echo
echo "Logs: sudo journalctl -u ncr7198-bridge -f"
