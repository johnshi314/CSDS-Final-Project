"""
Lightweight frontend server (stdlib only, no extra dependencies).
Serves index.html on port 3000 and proxies /api/* requests to the
auth server on port 8000.
"""

import http.server
import json
import os
import urllib.request
import urllib.error
from pathlib import Path

PORT = int(os.getenv("FRONTEND_PORT", 3000))
AUTH_BACKEND = os.getenv("AUTH_BACKEND", "http://127.0.0.1:8000")
STATIC_DIR = Path(__file__).parent


class FrontendHandler(http.server.BaseHTTPRequestHandler):

    def do_GET(self):
        if self.path == "/" or self.path == "/index.html":
            self._serve_file(STATIC_DIR / "index.html", "text/html")
        else:
            self.send_error(404)

    def do_POST(self):
        if self.path.startswith("/api/"):
            self._proxy_to_backend()
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

    def _proxy_to_backend(self):
        backend_path = self.path[len("/api"):]  # strip /api prefix
        url = AUTH_BACKEND + backend_path

        content_length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(content_length) if content_length else b""

        req = urllib.request.Request(
            url,
            data=body,
            headers={"Content-Type": "application/json"},
            method="POST",
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
    server = http.server.HTTPServer(("0.0.0.0", PORT), FrontendHandler)
    print(f"Frontend server running on http://0.0.0.0:{PORT}")
    print(f"Proxying /api/* -> {AUTH_BACKEND}")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nShutting down frontend server.")
        server.server_close()
