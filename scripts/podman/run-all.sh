#!/usr/bin/env bash
# Restart both netflower systemd user services.
# Build images first with build.sh if code has changed.
set -euo pipefail
DIR="$(cd "$(dirname "$0")" && pwd)"

"$DIR/run-server.sh"
"$DIR/run-frontend.sh"
