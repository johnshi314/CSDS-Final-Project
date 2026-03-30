"""
Lightweight frontend server (stdlib only, no extra dependencies).
Serves index.html on port 3000 and proxies /api/* requests to the
auth server on port 8000.
"""

import http.server
import json
import os
import signal
import sys
import urllib.request
import urllib.error
from pathlib import Path

PORT = int(os.getenv("FRONTEND_PORT", 3000))
AUTH_BACKEND = os.getenv("AUTH_BACKEND", "http://127.0.0.1:8000")
# When 0, forward full path (e.g. /api/login → backend .../api/login) for API_PREFIX=/api.
AUTH_STRIP_API_PREFIX = os.getenv("AUTH_STRIP_API_PREFIX", "1").lower() in ("1", "true", "yes")
STATIC_DIR = Path(__file__).parent

_tee_files: list[object] = []


def _default_log_dir() -> Path:
    """Repo root logs/ when run from tree; /app/logs in the slim container layout."""
    here = Path(__file__).resolve().parent
    repo_root = here.parent.parent
    if (repo_root / "requirements.txt").is_file():
        return repo_root / "logs"
    return here / "logs"


def _stream_tee_enabled() -> bool:
    return os.getenv("PYTHON_STREAM_TEE", "1").strip().lower() not in ("0", "false", "no", "off")


class _TeeIO:
    __slots__ = ("_primary", "_file")

    def __init__(self, primary, fileobj):
        self._primary = primary
        self._file = fileobj

    def write(self, data):
        if not data:
            return 0
        self._primary.write(data)
        self._primary.flush()
        self._file.write(data)
        self._file.flush()
        return len(data)

    def flush(self):
        self._primary.flush()
        self._file.flush()

    def isatty(self):
        return self._primary.isatty()

    def fileno(self):
        return self._primary.fileno()


def _install_stream_tee(process_name: str = "frontend") -> None:
    if not _stream_tee_enabled() or isinstance(sys.stdout, _TeeIO):
        return
    raw = os.getenv("LOG_DIR", "").strip()
    if raw:
        log_dir = Path(raw).resolve()
    else:
        log_dir = _default_log_dir().resolve()
    log_dir.mkdir(parents=True, exist_ok=True)
    out_f = open(log_dir / f"{process_name}.stdout.log", "a", encoding="utf-8", buffering=1)
    err_f = open(log_dir / f"{process_name}.stderr.log", "a", encoding="utf-8", buffering=1)
    _tee_files.extend([out_f, err_f])
    sys.stdout = _TeeIO(sys.__stdout__, out_f)
    sys.stderr = _TeeIO(sys.__stderr__, err_f)


class FrontendHandler(http.server.BaseHTTPRequestHandler):

    def do_GET(self):
        if self.path == "/" or self.path == "/index.html":
            self._serve_file(STATIC_DIR / "index.html", "text/html")
        elif self.path.startswith("/api/"):
            self._proxy_to_backend("GET")
        else:
            self.send_error(404)

    def do_POST(self):
        if self.path.startswith("/api/"):
            self._proxy_to_backend("POST")
        else:
            self.send_error(404)

    def _serve_file(self, filepath, content_type):
        try:
            data = filepath.read_bytes()
            self.send_response(200)
            self.send_header("Content-Type", content_type)
            self.send_header("Content-Length", len(data))
            self.end_headers()
            self.wfile.write(data)
        except FileNotFoundError:
            self.send_error(404)

    def _proxy_to_backend(self, method: str):
        if AUTH_STRIP_API_PREFIX and self.path.startswith("/api"):
            backend_path = self.path[len("/api"):]  # /api/login → /login (legacy local backend)
            url = AUTH_BACKEND.rstrip("/") + backend_path
        else:
            url = AUTH_BACKEND.rstrip("/") + self.path

        content_length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(content_length) if content_length else b""

        forward_headers = {}
        for name in ("Content-Type", "Authorization", "Cookie", "Accept"):
            value = self.headers.get(name)
            if value:
                forward_headers[name] = value

        if method == "POST" and "Content-Type" not in forward_headers:
            forward_headers["Content-Type"] = "application/json"

        req = urllib.request.Request(
            url,
            data=body if method == "POST" else None,
            headers=forward_headers,
            method=method,
        )

        try:
            with urllib.request.urlopen(req) as resp:
                resp_body = resp.read()
                self.send_response(resp.status)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", len(resp_body))
                self.end_headers()
                self.wfile.write(resp_body)
        except urllib.error.HTTPError as e:
            resp_body = e.read()
            self.send_response(e.code)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", len(resp_body))
            self.end_headers()
            self.wfile.write(resp_body)
        except urllib.error.URLError:
            err = json.dumps({"detail": "Auth server unavailable"}).encode()
            self.send_response(502)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", len(err))
            self.end_headers()
            self.wfile.write(err)

    def log_message(self, format, *args):
        print(f"[frontend] {self.address_string()} - {format % args}")


if __name__ == "__main__":
    _install_stream_tee("frontend")
    server = http.server.HTTPServer(("0.0.0.0", PORT), FrontendHandler)
    signal.signal(signal.SIGTERM, lambda _sig, _frame: server.shutdown())
    print(f"Frontend server running on http://0.0.0.0:{PORT}")
    print(f"Proxying /api/* -> {AUTH_BACKEND}")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        print("\nShutting down frontend server.")
        server.server_close()
