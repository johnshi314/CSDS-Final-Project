#!/usr/bin/env bash
# Build API and/or frontend images, then prune dangling old images.
# Usage: ./build.sh [all|server|frontend|webgl]   (default: all)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

TARGET="${1:-all}"
SERVER_TAG="${SERVER_IMAGE:-localhost/netflower-server:latest}"
FRONTEND_TAG="${FRONTEND_IMAGE:-localhost/netflower-frontend:latest}"
WEBGL_TAG="${WEBGL_IMAGE:-localhost/netflower-webgl:latest}"

IG=()
if [[ -f scripts/podman/.containerignore ]]; then
  IG=(--ignorefile scripts/podman/.containerignore)
fi

build_server() {
  podman build "${IG[@]}" -f scripts/podman/Containerfile.server -t "$SERVER_TAG" .
  echo "Built $SERVER_TAG"
}

build_frontend() {
  podman build "${IG[@]}" -f scripts/podman/Containerfile.frontend -t "$FRONTEND_TAG" .
  echo "Built $FRONTEND_TAG"
}

build_webgl() {
  podman build "${IG[@]}" -f scripts/podman/Containerfile.webgl -t "$WEBGL_TAG" .
  echo "Built $WEBGL_TAG"
}

case "$TARGET" in
  all)
    build_server
    build_frontend
    build_webgl
    ;;
  server|api)
    build_server
    ;;
  frontend|fe)
    build_frontend
    ;;
  webgl|game)
    build_webgl
    ;;
  *)
    echo "Usage: $0 [all|server|frontend|webgl]" >&2
    exit 1
    ;;
esac

echo "Pruning dangling images..."
podman image prune -f
echo "Build complete ($TARGET)."
