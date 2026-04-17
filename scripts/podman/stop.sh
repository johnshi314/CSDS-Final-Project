#!/usr/bin/env bash
# Stop both netflower systemd user services.
set -euo pipefail
# shellcheck source=env-user-session.sh
source "$(cd "$(dirname "$0")" && pwd)/env-user-session.sh"

systemctl --user stop netflower-frontend.service 2>/dev/null && echo "Stopped frontend" || echo "Frontend was not running"
systemctl --user stop netflower-webgl.service   2>/dev/null && echo "Stopped webgl"    || echo "WebGL was not running"
systemctl --user stop netflower-api.service      2>/dev/null && echo "Stopped API"      || echo "API was not running"

# Clear any "failed" states left over from non-zero container exit codes
systemctl --user reset-failed netflower-frontend.service 2>/dev/null || true
systemctl --user reset-failed netflower-webgl.service   2>/dev/null || true
systemctl --user reset-failed netflower-api.service      2>/dev/null || true

# Purge ghosts from old podman-generate-systemd services (safe no-op if already gone)
for old in container-netflower-api container-netflower-frontend; do
  systemctl --user reset-failed "$old.service" 2>/dev/null || true
done
