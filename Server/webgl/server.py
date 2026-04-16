"""
Serve a Unity WebGL build from a directory (e.g. Builds/Netflower with index.html).

Stdlib only. Intended to bind on loopback; nginx terminates TLS and proxies
https://www.example.com/game/ -> http://127.0.0.1:3001/ with path stripping.

Environment (see ``.env.example``):
  WEBGL_ROOT   - folder containing index.html (default: Builds/Netflower under repo root when detected)
  WEBGL_PORT   - listen port (default: 3001)
  WEBGL_HOST   - bind address (default: 127.0.0.1; use 0.0.0.0 in containers)
  LOG_DIR, PYTHON_STREAM_TEE

Run from repo root: ``python -m Server.webgl`` (loads ``.env`` via ``Server`` package).
"""

from __future__ import annotations

import http.server
import mimetypes
import os
import signal
import sys
from pathlib import Path
from urllib.parse import unquote, urlparse

# Unity / compressed outputs
mimetypes.add_type("application/wasm", ".wasm")
mimetypes.add_type("application/javascript", ".js")
mimetypes.add_type("application/json", ".json")
mimetypes.add_type("application/octet-stream", ".data")
mimetypes.add_type("application/octet-stream", ".unityweb")


def _default_webgl_root() -> Path:
    here = Path(__file__).resolve().parent
    repo_root = here.parent.parent
    if (repo_root / "requirements.txt").is_file():
        candidate = repo_root / "Builds" / "Netflower"
        if candidate.is_dir():
            return candidate
        return candidate
    return Path("/webgl")


def _default_log_dir() -> Path:
    here = Path(__file__).resolve().parent
    repo_root = here.parent.parent
    if (repo_root / "requirements.txt").is_file():
        return repo_root / "logs"
    return Path("/app/logs")


_tee_files: list[object] = []


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


def _stream_tee_enabled() -> bool:
    return os.getenv("PYTHON_STREAM_TEE", "1").strip().lower() not in ("0", "false", "no", "off")


def _install_stream_tee(process_name: str = "webgl") -> None:
    if not _stream_tee_enabled() or isinstance(sys.stdout, _TeeIO):
        return
    raw = os.getenv("LOG_DIR", "").strip()
    log_dir = Path(raw).resolve() if raw else _default_log_dir().resolve()
    log_dir.mkdir(parents=True, exist_ok=True)
    out_f = open(log_dir / f"{process_name}.stdout.log", "a", encoding="utf-8", buffering=1)
    err_f = open(log_dir / f"{process_name}.stderr.log", "a", encoding="utf-8", buffering=1)
    _tee_files.extend([out_f, err_f])
    sys.stdout = _TeeIO(sys.__stdout__, out_f)
    sys.stderr = _TeeIO(sys.__stderr__, err_f)


def _safe_path(web_root: Path, url_path: str) -> Path | None:
    """Map URL path to file under web_root; reject traversal."""
    parsed = urlparse(url_path)
    rel = unquote(parsed.path).lstrip("/")
    if not rel:
        rel = "index.html"
    elif rel.endswith("/"):
        rel = rel.rstrip("/") + "/index.html"
    candidate = (web_root / rel).resolve()
    try:
        candidate.relative_to(web_root.resolve())
    except ValueError:
        return None
    return candidate


def _content_type_and_encoding(file_path: Path) -> tuple[str, str | None]:
    """Guess Content-Type; handle precompressed .br / .gz Unity artifacts."""
    p = file_path
    encoding: str | None = None
    # Strip outermost compression suffix first (Unity: Netflower.wasm.br, etc.)
    while p.suffix.lower() == ".br":
        encoding = "br"
        p = p.with_suffix("")
    if p.suffix.lower() == ".gz":
        encoding = "gzip"
        p = p.with_suffix("")

    name_lower = p.name.lower()
    # Browsers require application/wasm for streaming compile; do not rely on guess_type alone.
    if name_lower.endswith(".wasm"):
        return "application/wasm", encoding
    if name_lower.endswith(".js"):
        return "application/javascript", encoding
    if name_lower.endswith(".json"):
        return "application/json", encoding
    if name_lower.endswith(".data") or name_lower.endswith(".unityweb"):
        return "application/octet-stream", encoding

    ctype, _ = mimetypes.guess_type(str(p))
    if not ctype:
        ctype = "application/octet-stream"
    return ctype, encoding


class WebGLHandler(http.server.BaseHTTPRequestHandler):
    web_root: Path

    def do_GET(self):
        if self.path.split("?", 1)[0] in ("/health", "/health/"):
            body = b'{"status":"ok"}'
            self.send_response(200)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
            return

        path = _safe_path(self.web_root, self.path)
        if path is None or not path.is_file():
            self.send_error(404)
            return

        try:
            data = path.read_bytes()
        except OSError:
            self.send_error(500)
            return

        ctype, enc = _content_type_and_encoding(path)
        self.send_response(200)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(data)))
        if enc:
            self.send_header("Content-Encoding", enc)
        # Safe caching for versioned Unity output under Build/
        rel = path.relative_to(self.web_root)
        if len(rel.parts) > 0 and rel.parts[0] == "Build":
            self.send_header("Cache-Control", "public, max-age=31536000, immutable")
        self.end_headers()
        self.wfile.write(data)

    def log_message(self, fmt, *args):
        print(f"[webgl] {self.address_string()} - {fmt % args}")


def main() -> None:
    _install_stream_tee("webgl")
    root = Path(os.getenv("WEBGL_ROOT", str(_default_webgl_root()))).resolve()
    if not root.is_dir():
        print(f"[webgl] ERROR: WEBGL_ROOT is not a directory: {root}", file=sys.stderr)
        sys.exit(1)
    index = root / "index.html"
    if not index.is_file():
        print(f"[webgl] WARNING: no index.html at {index} (upload WebGL build here)", file=sys.stderr)

    port = int(os.getenv("WEBGL_PORT", "3001"))
    host = os.getenv("WEBGL_HOST", "127.0.0.1")

    WebGLHandler.web_root = root

    class BoundHandler(WebGLHandler):
        pass

    server = http.server.ThreadingHTTPServer((host, port), BoundHandler)
    signal.signal(signal.SIGTERM, lambda _s, _f: server.shutdown())
    print(f"[webgl] Serving {root} at http://{host}:{port}/")
    print(f"[webgl] Health: http://{host}:{port}/health")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()
        print("\n[webgl] Shut down.")
