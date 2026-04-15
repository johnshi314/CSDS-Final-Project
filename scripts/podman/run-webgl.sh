#!/usr/bin/env bash
# Restart the netflower-webgl systemd user service.
# Rebuild the image first: ./scripts/podman/build.sh webgl
set -euo pipefail
export XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-/run/user/$(id -u)}"
export DBUS_SESSION_BUS_ADDRESS="${DBUS_SESSION_BUS_ADDRESS:-unix:path=$XDG_RUNTIME_DIR/bus}"

SERVICE="netflower-webgl.service"

systemctl --user restart "$SERVICE"
echo "Restarted $SERVICE"
systemctl --user --no-pager status "$SERVICE"
