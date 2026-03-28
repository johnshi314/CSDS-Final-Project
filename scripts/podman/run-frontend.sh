#!/usr/bin/env bash
# Run static + /api proxy on 127.0.0.1:3000 (default FRONTEND_PORT in server.py).
# Point nginx at http://127.0.0.1:3000 for the web app if you proxy the frontend.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

LOG_DIR_HOST="${NETFLOWER_LOG_DIR:-$ROOT/logs}"
mkdir -p "$LOG_DIR_HOST"

NETWORK="${NETFLOWER_NETWORK:-netflower}"
API_NAME="${NETFLOWER_API_CONTAINER:-netflower-api}"
FE_NAME="${NETFLOWER_FRONTEND_CONTAINER:-netflower-frontend}"
FRONTEND_TAG="${FRONTEND_IMAGE:-localhost/netflower-frontend:latest}"
HOST_PORT="${FRONTEND_HOST_PORT:-3000}"
CONTAINER_PORT="${FRONTEND_PORT:-3000}"

if ! podman network inspect "$NETWORK" &>/dev/null; then
  podman network create "$NETWORK"
fi

# Backend uses API_PREFIX=/api — do not strip /api when proxying from this dev server.
AUTH_BACKEND="${AUTH_BACKEND:-http://${API_NAME}:8000}"

podman run -d --replace --name "$FE_NAME" \
  --network "$NETWORK" \
  -e FRONTEND_PORT="$CONTAINER_PORT" \
  -e AUTH_BACKEND="$AUTH_BACKEND" \
  -e AUTH_STRIP_API_PREFIX="${AUTH_STRIP_API_PREFIX:-0}" \
  -e LOG_DIR=/app/logs \
  -v "$LOG_DIR_HOST:/app/logs:z" \
  -p "127.0.0.1:${HOST_PORT}:${CONTAINER_PORT}" \
  "$FRONTEND_TAG"

echo "Frontend container $FE_NAME on http://127.0.0.1:${HOST_PORT} → AUTH_BACKEND=$AUTH_BACKEND"
echo "Logs: $LOG_DIR_HOST"
