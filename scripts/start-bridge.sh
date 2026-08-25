#!/bin/sh
set -eu

if [ "${Bridge__Transport:-Device}" = "Device" ]; then
    /usr/bin/stty -F "$Bridge__DevicePath" raw -echo -ixon -ixoff -crtscts
fi
exec /opt/ncr7198-bridge/Ncr7198.PiBridge
