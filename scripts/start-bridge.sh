#!/bin/sh
set -eu

if [ "${Bridge__Transport:-Device}" = "Device" ] && [ -c "$Bridge__DevicePath" ]; then
    if ! /usr/bin/stty -F "$Bridge__DevicePath" raw -echo -ixon -ixoff -crtscts; then
        echo "Warning: could not configure $Bridge__DevicePath; the bridge will stay online and report the printer unavailable." >&2
    fi
fi
exec /opt/ncr7198-bridge/Ncr7198.PiBridge
