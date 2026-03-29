##############################################################
# File: Server/__main__.py
# Description: Run the unified HTTP + lobby WebSocket server (FastAPI).
# Optional: run turn-based demo WebSocket on port 8765 in a second terminal.
##############################################################
"""
Run the game backend: HTTP (auth, stats, lobby REST) + lobby WebSocket on the same port.

    python -m Server

Uses one process so lobby state and WebSocket fan-out share memory. For the separate
turn-based echo demo (epoch/turn protocol), run in another terminal:

    python -m Server.multiplayer_echo

Environment (optional):

- UVICORN_RELOAD — if 1/true (default), restart workers when Python files under
  Server/ or Database/ change. Set to 0 in production.
- SERVER_SUPERVISE — if 1/true, run the app in a child process and restart it if
  the process exits with an error or if GET http://127.0.0.1:{port}/health fails
  repeatedly (see WATCHDOG_* vars below). The child never supervises (avoids loops).
- WATCHDOG_INTERVAL_SEC — seconds between health checks (default 15).
- WATCHDOG_FAIL_THRESHOLD — consecutive failures before SIGTERM + restart (default 3).
- WATCHDOG_STARTUP_GRACE_SEC — no failed counts until this long after spawn (default 12).
"""
import os
import signal
import subprocess
import sys
import threading
import time
import urllib.error
import urllib.request
from pathlib import Path


def _truthy(name: str, default: str = "0") -> bool:
    return os.getenv(name, default).strip().lower() in ("1", "true", "yes", "on")


def _run_uvicorn() -> None:
    import uvicorn
    from logging_config import get_logger

    logger = get_logger(__name__)
    host = os.getenv("AUTH_SERVER_HOST", "0.0.0.0")
    port = int(os.getenv("AUTH_SERVER_PORT", "8000"))
    reload = _truthy("UVICORN_RELOAD", "1")
    pkg_dir = Path(__file__).resolve().parent
    repo_root = pkg_dir.parent
    reload_dirs = [str(pkg_dir), str(repo_root / "Database")] if reload else None

    if reload:
        logger.info(
            "Uvicorn auto-reload enabled for Server/ and Database/ (set UVICORN_RELOAD=0 to disable)"
        )
    logger.info(
        "Starting unified server on http://%s:%s (lobby WS: /ws/lobby/{match_id})",
        host,
        port,
    )
    kwargs = dict(
        host=host,
        port=port,
        log_level="info",
        use_colors=sys.stderr.isatty(),
        ws_ping_interval=20,
        ws_ping_timeout=10,
    )
    if reload:
        kwargs["reload"] = True
        kwargs["reload_dirs"] = reload_dirs
    uvicorn.run("Server.auth_http:app", **kwargs)


def _run_supervised() -> None:
    from logging_config import get_logger

    logger = get_logger(__name__)
    host = os.getenv("AUTH_SERVER_HOST", "0.0.0.0")
    port = int(os.getenv("AUTH_SERVER_PORT", "8000"))
    interval = float(os.getenv("WATCHDOG_INTERVAL_SEC", "15"))
    fail_limit = int(os.getenv("WATCHDOG_FAIL_THRESHOLD", "3"))
    grace = float(os.getenv("WATCHDOG_STARTUP_GRACE_SEC", "12"))
    health_url = f"http://127.0.0.1:{port}/health"

    state: dict = {"proc": None, "shutdown": False}

    def on_signal(signum, frame):
        state["shutdown"] = True
        p = state["proc"]
        if p is not None and p.poll() is None:
            p.terminate()

    signal.signal(signal.SIGINT, on_signal)
    signal.signal(signal.SIGTERM, on_signal)

    repo_root = Path(__file__).resolve().parent.parent
    logger.info(
        "Supervisor mode: child server + health checks to %s (set SERVER_SUPERVISE=0 to disable)",
        health_url,
    )

    while not state["shutdown"]:
        env = os.environ.copy()
        env["SERVER_SUPERVISE"] = "0"
        proc = subprocess.Popen(
            [sys.executable, "-m", "Server"],
            env=env,
            cwd=str(repo_root),
        )
        state["proc"] = proc
        stop_monitor = threading.Event()
        failures = [0]

        def monitor():
            time.sleep(grace)
            while not stop_monitor.is_set() and proc.poll() is None:
                try:
                    req = urllib.request.Request(health_url, method="GET")
                    with urllib.request.urlopen(req, timeout=5) as resp:
                        if resp.status == 200:
                            failures[0] = 0
                except (urllib.error.URLError, TimeoutError, OSError):
                    failures[0] += 1
                    if failures[0] >= fail_limit:
                        logger.error(
                            "Health check failed %d times in a row; sending SIGTERM to server",
                            fail_limit,
                        )
                        proc.terminate()
                        break
                time.sleep(interval)

        mon = threading.Thread(target=monitor, daemon=True)
        mon.start()
        rc = proc.wait()
        stop_monitor.set()
        mon.join(timeout=min(interval, 5.0))
        state["proc"] = None

        if state["shutdown"]:
            sys.exit(0)
        if rc == 0:
            logger.info("Server process exited cleanly; supervisor stopping")
            sys.exit(0)
        logger.warning("Server process exited with code %s; restarting in 2s", rc)
        time.sleep(2.0)


if __name__ == "__main__":
    from logging_config import install_stream_tee

    install_stream_tee("server")

    if _truthy("SERVER_SUPERVISE"):
        _run_supervised()
    else:
        def _sig_exit(signum, frame):
            sys.exit(0)

        signal.signal(signal.SIGINT, _sig_exit)
        _run_uvicorn()
