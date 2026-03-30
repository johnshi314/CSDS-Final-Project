#!/usr/bin/env bash
# Rebuild images and restart the corresponding systemd user service(s).
# Usage: ./reload.sh [all|server|frontend]   (default: all)
#
# Faster when you only changed Python/API: ./reload.sh server
set -euo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"
TARGET="${1:-all}"

"$DIR/build.sh" "$TARGET"

case "$TARGET" in
  all)
    "$DIR/run-server.sh"
    "$DIR/run-frontend.sh"
    ;;
  server|api)
    "$DIR/run-server.sh"
    ;;
  frontend|fe)
    "$DIR/run-frontend.sh"
    ;;
  *)
    echo "Usage: $0 [all|server|frontend]" >&2
    exit 1
    ;;
esac

echo "Reload complete ($TARGET)."
