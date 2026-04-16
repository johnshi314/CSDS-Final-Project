#!/usr/bin/env bash
# Deploy the most recent WebGL build tarball from the Builds directory.
# Usage: ./deploy-webgl.sh
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
BUILDS="$ROOT/Builds"
DEST="$BUILDS/Netflower"

ARCHIVE=$(ls -t "$BUILDS"/*.tar.xz 2>/dev/null | head -1)
if [[ -z "$ARCHIVE" ]]; then
  echo "ERROR: No .tar.xz files found in $BUILDS" >&2
  exit 1
fi

echo "Latest archive: $(basename "$ARCHIVE")"

# Extract to a temp dir first so we can find the top-level folder name
TMPDIR=$(mktemp -d "$BUILDS/deploy-XXXXXX")
trap 'rm -rf "$TMPDIR"' EXIT

tar -xJf "$ARCHIVE" -C "$TMPDIR"

EXTRACTED=$(ls -d "$TMPDIR"/*/ 2>/dev/null | head -1)
if [[ -z "$EXTRACTED" ]]; then
  echo "ERROR: Archive didn't contain a directory" >&2
  exit 1
fi

echo "Extracted folder: $(basename "$EXTRACTED")"

rm -rf "$DEST"
mv "$EXTRACTED" "$DEST"

chmod -R a+rX "$DEST"
restorecon -Rv "$DEST" 2>/dev/null || true

echo "Deployed to $DEST"
echo "Restarting WebGL service..."

DIR="$(cd "$(dirname "$0")" && pwd)"
"$DIR/run-webgl.sh"

echo "Done. Hard-refresh the browser to see the new build."
