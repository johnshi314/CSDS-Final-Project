#!/usr/bin/env bash
# Stop both netflower systemd user services.
set -euo pipefail
export XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-/run/user/$(id -u)}"
export DBUS_SESSION_BUS_ADDRESS="${DBUS_SESSION_BUS_ADDRESS:-unix:path=$XDG_RUNTIME_DIR/bus}"

systemctl --user stop netflower-frontend.service 2>/dev/null && echo "Stopped frontend" || echo "Frontend was not running"
systemctl --user stop netflower-api.service      2>/dev/null && echo "Stopped API"      || echo "API was not running"
