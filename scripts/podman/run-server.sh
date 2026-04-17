#!/usr/bin/env bash
# Restart the netflower-api systemd user service.
# The service unit references localhost/netflower-server:latest,
# so rebuild the image first (build.sh server) to pick up code changes.
set -euo pipefail
# shellcheck source=env-user-session.sh
source "$(cd "$(dirname "$0")" && pwd)/env-user-session.sh"

SERVICE="netflower-api.service"

systemctl --user restart "$SERVICE"
echo "Restarted $SERVICE"
systemctl --user --no-pager status "$SERVICE"
