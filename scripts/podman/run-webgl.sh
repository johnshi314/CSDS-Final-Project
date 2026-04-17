#!/usr/bin/env bash
# Restart the netflower-webgl systemd user service.
# Rebuild the image first: ./scripts/podman/build.sh webgl
set -euo pipefail
# shellcheck source=env-user-session.sh
source "$(cd "$(dirname "$0")" && pwd)/env-user-session.sh"

SERVICE="netflower-webgl.service"

systemctl --user restart "$SERVICE"
echo "Restarted $SERVICE"
systemctl --user --no-pager status "$SERVICE"
