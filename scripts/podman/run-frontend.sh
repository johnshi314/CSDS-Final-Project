#!/usr/bin/env bash
# Run static + /api proxy on 127.0.0.1:3000.
# Uses --network=host so it can reach the API at localhost:8000.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

LOG_DIR_HOST="${NETFLOWER_LOG_DIR:-$ROOT/logs}"
mkdir -p "$LOG_DIR_HOST"

FE_NAME="${NETFLOWER_FRONTEND_CONTAINER:-netflower-frontend}"
FRONTEND_TAG="${FRONTEND_IMAGE:-localhost/netflower-frontend:latest}"
CONTAINER_PORT="${FRONTEND_PORT:-3000}"

AUTH_BACKEND="${AUTH_BACKEND:-http://127.0.0.1:8000}"

podman run -d --replace --name "$FE_NAME" \
  --network host \
  -e FRONTEND_PORT="$CONTAINER_PORT" \
  -e AUTH_BACKEND="$AUTH_BACKEND" \
  -e AUTH_STRIP_API_PREFIX="${AUTH_STRIP_API_PREFIX:-0}" \
  -e LOG_DIR=/app/logs \
  -v "$LOG_DIR_HOST:/app/logs:z" \
  "$FRONTEND_TAG"

echo "Frontend container $FE_NAME on http://127.0.0.1:${CONTAINER_PORT} → AUTH_BACKEND=$AUTH_BACKEND (host network)"
echo "Logs: $LOG_DIR_HOST"
