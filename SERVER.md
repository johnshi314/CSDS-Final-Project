# Game server (HTTP + WebSockets)

## Public layout (nginx on litecoders.com)

| Traffic | Public URL | Proxied to uvicorn (example) |
|--------|------------|-------------------------------|
| Web app | `https://litecoders.com` | Static / frontend |
| REST API | `https://litecoders.com/api/*` | `http://127.0.0.1:8000/api/*` |
| Lobby snapshot WebSocket | `wss://litecoders.com/ws/lobby/{match_id}?authToken=` | `http://127.0.0.1:8000/ws/lobby/...` |
| Lobby control WebSocket | `wss://litecoders.com/ws/lobby-control?authToken=` | `http://127.0.0.1:8000/ws/lobby-control` |
| Battle WebSocket | `wss://litecoders.com/ws/battle/{match_id}?authToken=` | `http://127.0.0.1:8000/ws/battle/...` |
| Unity WebGL game | `https://www.litecoders.com/game/` | `http://127.0.0.1:3001/` (static server; see below) |

**nginx WebSockets:** For `/ws/*` (lobby + battle), the upstream must allow the upgrade handshake, for example:

```nginx
location /ws/ {
    proxy_pass http://127.0.0.1:8000;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
}
```

Without `Upgrade` / `Connection`, browsers cannot open `wss://` to FastAPI. **WebGL builds cannot use `System.Net.WebSockets.ClientWebSocket`**; the game uses the **NativeWebSocket** package (browser WebSocket API) for lobby and battle sockets.

**WebGL `.wasm` MIME:** If the console reports `Incorrect response MIME type. Expected 'application/wasm'`, the browser got a non-`application/wasm` `Content-Type` for the `.wasm` / `.wasm.br` request. The Python `Server.webgl` handler forces `application/wasm` after stripping `.br` (see `_content_type_and_encoding`). **Production check:** `curl -sI 'https://…/game/Build/*.wasm.br'` must show `Content-Type: application/wasm`. If you see `application/octet-stream`, nginx is overriding the type (common when files are served statically).

If nginx **serves the game tree directly**, add a rule *before* generic static handling so `.wasm` and `.wasm.br` are not treated as generic binary:

```nginx
location ~* ^/game/.*\.wasm(\.br)?$ {
    default_type application/wasm;
}
```

Keep any existing `Content-Encoding: br` handling you already use for `.wasm.br` files; this block only fixes the MIME type.

If you **proxy** to `python -m Server.webgl` on port 3001, prefer passing through the upstream `Content-Type` unchanged (do not `default_type` override on that `location`).

For `.js` / `.js.br`, `types { application/javascript js; }` in `http` or `server` is usually enough once `.br` is stripped for MIME guessing—or proxy to Python.

Set environment on the **Python** host (recommended: one **`.env`** file at the repository root):

```bash
cp .env.example .env   # then edit secrets and ports
```

All bundled Python entrypoints load that file via `repo_dotenv.py` (variables already exported in the shell take precedence). In Podman, mount the same file as **`/app/.env`** in each container, or set the same keys in your Quadlet `Environment=` lines.

Typical production keys:

```bash
API_PREFIX=/api
WS_PREFIX=/ws
```

Leave **`API_PREFIX` unset** for local dev so routes stay at `http://localhost:8000/login` (no `/api`).

**`WS_PREFIX`** defaults to `/ws` if unset; lobby sockets are always `{WS_PREFIX}/lobby/{match_id}`.

## Unity (`Match` component)

| Build | `httpApiBaseUrl` | `lobbyWebSocketBaseUrl` |
|-------|------------------|-------------------------|
| Local | `http://localhost:8000` | *(empty)* -> `ws://localhost:8000/ws` |
| Production | `https://litecoders.com/api` | *(empty)* -> `wss://litecoders.com/ws` |

If your WS public URL ever differs from the usual "same host as the API + `/ws`", set **`lobbyWebSocketBaseUrl`** explicitly (e.g. `wss://litecoders.com/ws`, no trailing slash).

---

## Architecture (demo scale)

- **API process** (`python -m Server.api`) runs FastAPI on port **8000**: auth, match stats, lobby WebSockets, and battle WebSocket. For local dev, **`python -m Server`** starts API + frontend + WebGL together (see *Local laptop* below).
- **MySQL** holds `lobby_players` and `matches.lobby_status`. Apply `Database/migrations/001_lobby.sql` once.

## REST (relative to `httpApiBaseUrl`)

Lobby behavior is now WebSocket-only. REST remains for auth and match stats endpoints.

Full endpoint contract (methods, auth, JSON examples, and WebSocket actions): see [Server/API.md](Server/API.md).

## WebSocket

- Snapshot feed: `{WS_PREFIX}/lobby/{match_id}?authToken={jwt}` (default **`/ws/lobby/...`**).
- Lobby control: `{WS_PREFIX}/lobby-control?authToken={jwt}`.
- JSON actions: `joinNewLobby`, `subscribeLobby`, `setTeam`, `setReady`, `leaveLobby`, `snapshot`.
- Battle: `{WS_PREFIX}/battle/{match_id}?authToken={jwt}`.
- Text frames: `you|<playerId>`, `turn|<playerId>`, `said|<playerId>|<msg>`, `epoch|<n>`.

## Run (repo root)

Install dependencies only inside an **isolated environment** (conda or venv). Avoid `pip install` on your system / base conda Python.

**Conda** (env name: **`netflower`**). If the env already exists, only activate it:

```bash
conda activate netflower
pip install -r requirements.txt   # when deps change
python -m Server.api
```

Create the env once if needed:

```bash
conda create -n netflower python=3.12 -y
conda activate netflower
pip install -r requirements.txt
python -m Server.api
```

**venv:**

```bash
python3 -m venv .venv
source .venv/bin/activate   # Windows: .venv\Scripts\activate
pip install -r requirements.txt
python -m Server.api
```

At startup the log line shows the mounted HTTP prefix and WebSocket path.

### Local laptop (full stack)

Assume MySQL is already running and `Database/migrations` have been applied; set `DB_*` in `.env` (e.g. `DB_HOST=127.0.0.1`).

**One command (API + frontend + WebGL):** from the **repository root**:

```bash
python -m Server
```

This spawns three processes (same as the commands below). **Ctrl+C** stops all of them. If any service exits, the others are stopped. Child processes run with `SERVER_SUPERVISE=0` so you do not get nested supervisors.

**Or three terminals** (same services, easier log reading):

```bash
# Terminal 1 - API + WebSockets (default http://127.0.0.1:8000)
python -m Server.api

# Terminal 2 - static portal + /api proxy to the API (default http://127.0.0.1:3000)
python -m Server.frontend

# Terminal 3 - optional Unity WebGL build (default http://127.0.0.1:3001)
python -m Server.webgl
```

Override ports or `AUTH_BACKEND` / `WEBGL_ROOT` in `.env` as needed. Unity pointing at this machine should use `http://localhost:8000` for the API (see table above) unless you terminate TLS locally.

### Process options (environment)

| Variable | Default | Meaning |
|----------|---------|---------|
| `UVICORN_RELOAD` | `1` | Hot-restart when `.py` files change under `Server/` and `Database/`. Set to `0` in production. |
| `SERVER_SUPERVISE` | `0` | If `1`, wraps `python -m Server.api` in a parent that restarts the child after a crash or after repeated failures of `GET http://127.0.0.1:{port}/health`. |
| `WATCHDOG_INTERVAL_SEC` | `15` | Seconds between health checks when supervising. |
| `WATCHDOG_FAIL_THRESHOLD` | `3` | Consecutive failed checks before SIGTERM and restart. |
| `WATCHDOG_STARTUP_GRACE_SEC` | `12` | Ignore failures until this long after each child spawn. |

**`GET /health`** returns `{"status":"ok"}` without touching MySQL (same port as the API, path is always `/health`).

Example (dev with reload + crash/stuck recovery):

```bash
UVICORN_RELOAD=1 SERVER_SUPERVISE=1 python -m Server.api
```

Production typically uses `UVICORN_RELOAD=0` and relies on systemd or Docker for restarts instead of `SERVER_SUPERVISE`.

### Git push does not reload the server by itself

`git push` only updates the remote repository. Nothing on the host restarts until you **deploy** those commits (for example `git pull` on the machine, then rebuild/restart containers).

- **Local dev** (`python -m Server` or `python -m Server.api` on your laptop): with **`UVICORN_RELOAD=1`** (default in `.env.example`), changing `.py` files under `Server/` or `Database/` triggers a **uvicorn** process restart. That only applies to the filesystem the process is reading, not to "push" events. The **frontend** and **WebGL** helpers (`Server.frontend`, `Server.webgl`) use the stdlib HTTP server and do not watch files; restart them (or stop `python -m Server` and start again) when their code or static assets change.
- **Podman images** (`scripts/podman/Containerfile.server` sets **`UVICORN_RELOAD=0`**): the running container does not watch the repo for edits. After you pull new code on the server, run **`./scripts/podman/reload.sh`** (or the matching `run-*.sh`) so the image is rebuilt and the unit/container is recreated.
- **`SERVER_SUPERVISE` / health watchdog**: restarts the API child after crashes or repeated failed **`GET /health`** checks. It does not pull git or reload on commit.

---

## Podman (same ports as nginx examples)

Scripts live under **`scripts/podman/`**. They bind only on **loopback** so your host nginx can keep using:

| Upstream | Host bind | Matches |
|----------|-----------|---------|
| API + WebSocket | `127.0.0.1:8000` | `http://127.0.0.1:8000/api/*` and `/ws/...` |
| Web frontend | `127.0.0.1:3000` | Proxy your site root to this if you use the Python frontend |
| WebGL static | `127.0.0.1:3001` | e.g. `location /game/` -> `http://127.0.0.1:3001/` |

```bash
./scripts/podman/build.sh
./scripts/podman/run-server.sh    # API_PREFIX=/api, WS_PREFIX=/ws, .env from repo root
./scripts/podman/run-frontend.sh  # optional; AUTH_BACKEND points at API container on podman network
# or: ./scripts/podman/run-all.sh
```

**MySQL on the host:** set `DB_HOST=host.containers.internal` in `.env` (the API container maps `host.containers.internal` to the host gateway). If you only serve HTML via nginx from disk, you can skip `run-frontend.sh` and still use `run-server.sh` for `:8000`.

**Override ports (must match nginx):** `AUTH_SERVER_PORT=8000 ./scripts/podman/run-server.sh` (default), `FRONTEND_HOST_PORT=3000 ./scripts/podman/run-frontend.sh` (default).

**Stop:** `./scripts/podman/stop.sh`

**After code changes:** rebuild the image and recreate containers (same `.env` and ports):

```bash
./scripts/podman/reload.sh              # API + frontend (+ WebGL if Quadlet unit exists)
./scripts/podman/reload.sh server       # only Server/ + Database/ (faster)
./scripts/podman/reload.sh frontend     # only Server/frontend/
./scripts/podman/reload.sh webgl        # only Server/webgl/ (Unity static host)
```

`run-*.sh` uses `podman run --replace`, so the old container is replaced by one running the newly built image.

---

## Unity WebGL (`/game`)

The game build is **not** in git: deploy `index.html` and the `Build/` folder under the repo's **`Builds/Netflower/`** on the server (or any directory you mount read-only into the container).

1. **Image:** `scripts/podman/Containerfile.webgl` - static server via `python -m Server.webgl` (`Server/webgl/server.py`), default port **3001** on the host loopback.
2. **Quadlet:** copy `scripts/podman/netflower-webgl.container.example` to `~/.config/containers/systemd/netflower-webgl.container`, edit the `Volume=` line if your path differs, then:
   ```bash
   systemctl --user daemon-reload
   systemctl --user enable --now netflower-webgl.service
   ```
3. **Build / reload:** `./scripts/podman/build.sh webgl` then `./scripts/podman/run-webgl.sh`, or `./scripts/podman/reload.sh webgl`.
4. **Local test (no container):** from repo root, after a WebGL build exists at `Builds/Netflower/`:
   ```bash
   python -m Server.webgl
   ```
   (Set `WEBGL_ROOT`, `WEBGL_PORT`, `WEBGL_HOST` in `.env` if defaults are wrong.)

### nginx (`https://www.litecoders.com/game/`)

Proxy with a **trailing slash** on both sides so `/game/Build/foo.wasm` becomes `/Build/foo.wasm` on the app:

```nginx
location /game/ {
    proxy_pass http://127.0.0.1:3001/;
    proxy_http_version 1.1;
    proxy_set_header Host              $host;
    proxy_set_header X-Real-IP         $remote_addr;
    proxy_set_header X-Forwarded-For     $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto   $scheme;
}
```

Visitors should open **`https://www.litecoders.com/game/`** (trailing slash) so relative asset URLs resolve under `/game/`. If the build was produced for a different public path, adjust Unity's **Player -> Publishing Settings** (or your template) so loader URLs match `/game/`.

**Health:** `GET http://127.0.0.1:3001/health` returns `{"status":"ok"}`.

**Logs:** Python writes under repo **`logs/`** (gitignored): structured `*_YYYYMMDD.log` from `logging_config`, plus **`server.stdout.log` / `server.stderr.log`** (and **`frontend.*`** for the small web server). The same directory is bind-mounted at **`/app/logs`** in containers (`NETFLOWER_LOG_DIR` overrides the host path). Output still goes to the container's stdout/stderr so `podman logs` works. Set **`PYTHON_STREAM_TEE=0`** to skip the `.stdout.log` / `.stderr.log` files.
