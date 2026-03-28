#!/usr/bin/env bash
set -euo pipefail
API_NAME="${NETFLOWER_API_CONTAINER:-netflower-api}"
FE_NAME="${NETFLOWER_FRONTEND_CONTAINER:-netflower-frontend}"

podman stop "$FE_NAME" 2>/dev/null || true
podman stop "$API_NAME" 2>/dev/null || true
podman rm "$FE_NAME" 2>/dev/null || true
podman rm "$API_NAME" 2>/dev/null || true
echo "Stopped and removed $FE_NAME and $API_NAME (if they existed)."
