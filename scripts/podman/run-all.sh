#!/usr/bin/env bash
# Restart netflower systemd user services (API, frontend, WebGL if Quadlet unit exists).
# Build images first with build.sh if code has changed.
set -euo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"
QUADLET_DIR="${HOME}/.config/containers/systemd"

"$DIR/run-server.sh"
"$DIR/run-frontend.sh"
if [[ -f "$QUADLET_DIR/netflower-webgl.container" ]]; then
  "$DIR/run-webgl.sh"
fi
