#!/usr/bin/env bash
# Run FastAPI + lobby WebSocket on 127.0.0.1:8000 (matches SERVER.md nginx example).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

LOG_DIR_HOST="${NETFLOWER_LOG_DIR:-$ROOT/logs}"
mkdir -p "$LOG_DIR_HOST"

NETWORK="${NETFLOWER_NETWORK:-netflower}"
API_NAME="${NETFLOWER_API_CONTAINER:-netflower-api}"
SERVER_TAG="${SERVER_IMAGE:-localhost/netflower-server:latest}"
HOST_PORT="${AUTH_SERVER_PORT:-8000}"

if ! podman network inspect "$NETWORK" &>/dev/null; then
  podman network create "$NETWORK"
fi

ENV_FILE="${NETFLOWER_ENV_FILE:-$ROOT/.env}"
ENV_ARGS=()
if [[ -f "$ENV_FILE" ]]; then
  ENV_ARGS=(--env-file "$ENV_FILE")
else
  echo "Warning: no $ENV_FILE — set DB_* and JWT_SECRET or the API will fail." >&2
fi

podman run -d --replace --name "$API_NAME" \
  --network "$NETWORK" \
  "${ENV_ARGS[@]}" \
  --add-host host.containers.internal:host-gateway \
  -e API_PREFIX="${API_PREFIX:-/api}" \
  -e WS_PREFIX="${WS_PREFIX:-/ws}" \
  -e UVICORN_RELOAD="${UVICORN_RELOAD:-0}" \
  -e SERVER_SUPERVISE="${SERVER_SUPERVISE:-0}" \
  -e AUTH_SERVER_HOST=0.0.0.0 \
  -e AUTH_SERVER_PORT=8000 \
  -e LOG_DIR=/app/logs \
  -v "$LOG_DIR_HOST:/app/logs:z" \
  -p "127.0.0.1:${HOST_PORT}:8000" \
  "$SERVER_TAG"

echo "API container $API_NAME on http://127.0.0.1:${HOST_PORT} (network: $NETWORK)"
echo "Logs: $LOG_DIR_HOST (mounted at /app/logs in the container)"
echo "If MySQL runs on the host, use DB_HOST=host.containers.internal in .env for this setup."
