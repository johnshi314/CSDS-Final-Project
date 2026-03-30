#!/usr/bin/env bash
# Restart the netflower-frontend systemd user service.
# The service unit references localhost/netflower-frontend:latest,
# so rebuild the image first (build.sh frontend) to pick up code changes.
set -euo pipefail
export XDG_RUNTIME_DIR="${XDG_RUNTIME_DIR:-/run/user/$(id -u)}"
export DBUS_SESSION_BUS_ADDRESS="${DBUS_SESSION_BUS_ADDRESS:-unix:path=$XDG_RUNTIME_DIR/bus}"

SERVICE="netflower-frontend.service"

systemctl --user restart "$SERVICE"
echo "Restarted $SERVICE"
systemctl --user --no-pager status "$SERVICE"
