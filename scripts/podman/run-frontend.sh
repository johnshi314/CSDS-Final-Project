#!/usr/bin/env bash
# Restart the netflower-frontend systemd user service.
# The service unit references localhost/netflower-frontend:latest,
# so rebuild the image first (build.sh frontend) to pick up code changes.
set -euo pipefail
# shellcheck source=env-user-session.sh
source "$(cd "$(dirname "$0")" && pwd)/env-user-session.sh"

SERVICE="netflower-frontend.service"

systemctl --user restart "$SERVICE"
echo "Restarted $SERVICE"
systemctl --user --no-pager status "$SERVICE"
