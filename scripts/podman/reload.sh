#!/usr/bin/env bash
# Rebuild images and restart the corresponding systemd user service(s).
# Usage: ./reload.sh [all|server|frontend|webgl]   (default: all)
#
# Faster when you only changed Python/API: ./reload.sh server
set -euo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=env-user-session.sh
source "$DIR/env-user-session.sh"
TARGET="${1:-all}"
QUADLET_DIR="${HOME}/.config/containers/systemd"

"$DIR/build.sh" "$TARGET"

case "$TARGET" in
  all)
    "$DIR/run-server.sh"
    "$DIR/run-frontend.sh"
    if [[ -f "$QUADLET_DIR/netflower-webgl.container" ]]; then
      "$DIR/run-webgl.sh"
    else
      echo "Note: no $QUADLET_DIR/netflower-webgl.container - skipping WebGL service (see SERVER.md)."
    fi
    ;;
  server|api)
    "$DIR/run-server.sh"
    ;;
  frontend|fe)
    "$DIR/run-frontend.sh"
    ;;
  webgl|game)
    "$DIR/run-webgl.sh"
    ;;
  *)
    echo "Usage: $0 [all|server|frontend|webgl]" >&2
    exit 1
    ;;
esac

echo "Reload complete ($TARGET)."
