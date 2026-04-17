#!/usr/bin/env bash
# Normalize env for rootless Podman and systemctl --user after su/sudo/SSH quirks.
# Stale XDG_RUNTIME_DIR (e.g. still /run/user/ADMIN after `su netflower`) causes:
#   Failed to obtain podman configuration: mkdir /run/user/UID/libpod: permission denied
# Stale HOME breaks Quadlet paths (~/.config/containers/systemd).

PW_HOME="$(getent passwd "$(id -un)" | cut -d: -f6)"
if [[ -n "$PW_HOME" && "${HOME:-}" != "$PW_HOME" ]]; then
  echo "podman: overriding HOME (${HOME:-<unset>} -> $PW_HOME) for $(id -un)" >&2
  export HOME="$PW_HOME"
fi

U_RUNTIME="/run/user/$(id -u)"
if [[ ! -d "$U_RUNTIME" ]]; then
  echo "podman: user session directory missing: $U_RUNTIME" >&2
  echo "podman: use an interactive ssh login, or: sudo loginctl enable-linger $(id -un)" >&2
  exit 1
fi
if [[ -n "${XDG_RUNTIME_DIR:-}" && "$XDG_RUNTIME_DIR" != "$U_RUNTIME" ]]; then
  echo "podman: overriding XDG_RUNTIME_DIR ($XDG_RUNTIME_DIR -> $U_RUNTIME) for $(id -un) ($(id -u))" >&2
fi
export XDG_RUNTIME_DIR="$U_RUNTIME"
export DBUS_SESSION_BUS_ADDRESS="${DBUS_SESSION_BUS_ADDRESS:-unix:path=$XDG_RUNTIME_DIR/bus}"
