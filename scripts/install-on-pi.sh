#!/bin/sh
set -eu

if [ "$(id -u)" -ne 0 ]; then
    echo "Run this installer with sudo."
    exit 1
fi

NCR_DEVICE="${NCR_DEVICE:-}"
NCR_ALIAS=/dev/ncr7198
SOURCE_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
INSTALL_DIR=/opt/ncr7198-bridge
ENV_FILE=/etc/ncr7198-bridge.env
SERVICE_FILE=/etc/systemd/system/ncr7198-bridge.service
UDEV_RULE_FILE=/etc/udev/rules.d/70-ncr7198.rules

if [ ! -f "$SOURCE_DIR/70-ncr7198.rules" ]; then
    echo "70-ncr7198.rules is missing. Copy the complete Pi publish directory."
    exit 1
fi

if [ ! -f "$SOURCE_DIR/Ncr7198.PiBridge" ]; then
    echo "Ncr7198.PiBridge executable is missing. Run Publish-Pi.ps1 and copy the complete publish directory."
    exit 1
fi

is_ncr_device() {
    properties="$(udevadm info --query=property --name="$1" 2>/dev/null || true)"
    printf '%s\n' "$properties" | grep -q '^ID_VENDOR_ID=0404$' && \
        printf '%s\n' "$properties" | grep -q '^ID_MODEL_ID=0312$'
}

find_ncr_device() {
    for device in /dev/ttyUSB*; do
        [ -c "$device" ] || continue
        if is_ncr_device "$device"; then
            printf '%s\n' "$device"
            return 0
        fi
    done
    return 1
}

if [ -z "$NCR_DEVICE" ]; then
    NCR_DEVICE="$(find_ncr_device || true)"
fi

if [ -z "$NCR_DEVICE" ] || [ ! -c "$NCR_DEVICE" ]; then
    echo "NCR EPiC printer USB device 0404:0312 was not found."
    echo "Connect and power the printer, confirm it is in ION (EPiC) mode, then inspect: lsusb; ls -l /dev/ttyUSB*"
    exit 1
fi

if ! is_ncr_device "$NCR_DEVICE"; then
    echo "$NCR_DEVICE is not the expected NCR EPiC USB device 0404:0312."
    exit 1
fi

install -o root -g root -m 0644 "$SOURCE_DIR/70-ncr7198.rules" "$UDEV_RULE_FILE"
udevadm control --reload-rules
udevadm trigger --action=add --subsystem-match=tty
udevadm settle

if [ ! -c "$NCR_ALIAS" ]; then
    echo "The udev rule was installed, but $NCR_ALIAS was not created."
    echo "Unplug and reconnect the printer, then run this installer again."
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
    echo "Bridge__DevicePath=$NCR_ALIAS"
    echo "Bridge__Transport=Device"
    echo "Bridge__ListenUrl=http://0.0.0.0:80"
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
AmbientCapabilities=CAP_NET_BIND_SERVICE
CapabilityBoundingSet=CAP_NET_BIND_SERVICE
PrivateTmp=true
ProtectHome=true

[Install]
WantedBy=multi-user.target
UNIT

systemctl daemon-reload
systemctl enable ncr7198-bridge.service
systemctl restart ncr7198-bridge.service

PI_IP="$(hostname -I | awk '{print $1}')"
echo
echo "NCR 7198 bridge installed."
echo "Printer: $NCR_ALIAS -> $(readlink -f "$NCR_ALIAS")"
echo "Web UI:  http://$PI_IP/"
echo "Health:  http://$PI_IP/api/health"
echo
echo "Logs: sudo journalctl -u ncr7198-bridge -f"
