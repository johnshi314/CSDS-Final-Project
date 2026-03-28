#!/usr/bin/env bash
# Start API (127.0.0.1:8000) then frontend (127.0.0.1:3000). Build images first with build.sh.
set -euo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"
"$DIR/run-server.sh"
"$DIR/run-frontend.sh"
