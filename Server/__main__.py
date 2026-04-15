##############################################################
# File: Server/__main__.py
# Description: One command for local dev: API + frontend + WebGL together.
##############################################################
"""
Workstation convenience: run everything with one process group.

    python -m Server

Starts, in separate interpreter processes (same as opening three terminals):

- ``Server.api`` - HTTP + WebSockets (default ``http://127.0.0.1:8000``)
- ``Server.frontend`` - static portal + ``/api`` proxy (default ``:3000``)
- ``Server.webgl`` - Unity build folder (default ``:3001``)

Requires MySQL and ``.env`` like the individual commands. If any service exits,
the others are stopped.

For production or containers, run ``python -m Server.api`` (etc.) separately.
See SERVER.md.
"""
from __future__ import annotations

import os
import signal
import subprocess
import sys
import time
from pathlib import Path

_CHILD_ENV_EXTRA = {"SERVER_SUPERVISE": "0"}


def _repo_root() -> Path:
    return Path(__file__).resolve().parent.parent


def _child_env() -> dict[str, str]:
    env = os.environ.copy()
    env.update(_CHILD_ENV_EXTRA)
    return env


def _terminate_all(procs: list[subprocess.Popen], *, label: str) -> None:
    for p in procs:
        if p.poll() is None:
            p.terminate()
    for p in procs:
        try:
            p.wait(timeout=10)
        except subprocess.TimeoutExpired:
            p.kill()
            p.wait(timeout=5)
    if label:
        print(label, file=sys.stderr)


def main() -> None:
    root = _repo_root()
    if str(root) not in sys.path:
        sys.path.insert(0, str(root))
    from repo_dotenv import load_repo_dotenv

    load_repo_dotenv(base_dir=root)

    exe = sys.executable
    env = _child_env()
    names = ("api", "frontend", "webgl")
    modules = ("Server.api", "Server.frontend", "Server.webgl")

    print(
        "NetFlower dev stack - starting API, frontend, and WebGL.\n"
        "  API:       http://127.0.0.1:8000 (default)\n"
        "  Frontend:  http://127.0.0.1:3000\n"
        "  WebGL:     http://127.0.0.1:3001\n"
        "Press Ctrl+C to stop all.\n"
    )

    procs: list[subprocess.Popen] = [
        subprocess.Popen([exe, "-m", mod], cwd=str(root), env=env) for mod in modules
    ]

    def _on_signal(signum, _frame):
        _terminate_all(procs, label="")
        # 130 = 128 + SIGINT; 143 = 128 + SIGTERM (common shell convention)
        raise SystemExit(130 if signum == signal.SIGINT else 143 if signum == signal.SIGTERM else 1)

    signal.signal(signal.SIGINT, _on_signal)
    signal.signal(signal.SIGTERM, _on_signal)

    try:
        while True:
            for i, p in enumerate(procs):
                rc = p.poll()
                if rc is not None:
                    print(
                        f"\nService '{names[i]}' exited with code {rc}. Stopping the others...",
                        file=sys.stderr,
                    )
                    _terminate_all(procs, label="")
                    raise SystemExit(rc if rc != 0 else 0)
            time.sleep(0.25)
    except KeyboardInterrupt:
        print("\nStopping all services...", file=sys.stderr)
        _terminate_all(procs, label="")


if __name__ == "__main__":
    main()
