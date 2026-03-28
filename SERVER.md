# Game server (HTTP + lobby WebSocket)

## Public layout (nginx on litecoders.com)

| Traffic | Public URL | Proxied to uvicorn (example) |
|--------|------------|-------------------------------|
| Web app | `https://litecoders.com` | Static / frontend |
| REST API | `https://litecoders.com/api/*` | `http://127.0.0.1:8000/api/*` |
| Lobby WebSocket | `wss://litecoders.com/ws/lobby/{match_id}?player_id=` | `http://127.0.0.1:8000/ws/lobby/...` |

Set environment on the **Python** host:

```bash
API_PREFIX=/api
WS_PREFIX=/ws
```

Leave **`API_PREFIX` unset** for local dev so routes stay at `http://localhost:8000/join-new-lobby` (no `/api`).

**`WS_PREFIX`** defaults to `/ws` if unset; lobby sockets are always `{WS_PREFIX}/lobby/{match_id}`.

## Unity (`Match` component)

| Build | `httpApiBaseUrl` | `lobbyWebSocketBaseUrl` |
|-------|------------------|-------------------------|
| Local | `http://localhost:8000` | *(empty)* → `ws://localhost:8000/ws` |
| Production | `https://litecoders.com/api` | *(empty)* → `wss://litecoders.com/ws` |

If your WS public URL ever differs from “same host as API + `/ws`”, set **`lobbyWebSocketBaseUrl`** explicitly (e.g. `wss://litecoders.com/ws`, no trailing slash).

---

## Architecture (demo scale)

- **One Python process** (`python -m Server`) runs FastAPI on port **8000**: auth, match stats, lobby REST, lobby WebSocket.
- **MySQL** holds `lobby_players` and `matches.lobby_status`. Apply `Database/migrations/001_lobby.sql` once.
- **Optional:** `python -m Server.multiplayer_echo` — separate turn demo on port **8765**.

## REST (relative to `httpApiBaseUrl`)

| Path | Purpose |
|------|---------|
| `GET /join-new-lobby?player_id=` | Enter or create lobby |
| `GET /get-lobby-updates?match_id=` | Polling snapshot |
| `POST /set-player-team?...` | Pick team |
| `GET /set-ready?...` | Mark ready |

With **`API_PREFIX=/api`**, Unity calls `https://litecoders.com/api/join-new-lobby`, etc.

## WebSocket

- Path: `{WS_PREFIX}/lobby/{match_id}?player_id={id}` (default **`/ws/lobby/...`**).
- Messages: JSON snapshot (`everyoneReady`, `redTeamPlayerIds`, `blueTeamPlayerIds`).

## Run (repo root)

Install dependencies only inside an **isolated environment** (conda or venv). Avoid `pip install` on your system / base conda Python.

**Conda** (env name: **`netflower`**). If the env already exists, only activate it:

```bash
conda activate netflower
pip install -r requirements.txt   # when deps change
python -m Server
```

Create the env once if needed:

```bash
conda create -n netflower python=3.12 -y
conda activate netflower
pip install -r requirements.txt
python -m Server
```

**venv:**

```bash
python3 -m venv .venv
source .venv/bin/activate   # Windows: .venv\Scripts\activate
pip install -r requirements.txt
python -m Server
```

At startup the log line shows the mounted HTTP prefix and WebSocket path.

### Process options (environment)

| Variable | Default | Meaning |
|----------|---------|---------|
| `UVICORN_RELOAD` | `1` | Hot-restart when `.py` files change under `Server/` and `Database/`. Set to `0` in production. |
| `SERVER_SUPERVISE` | `0` | If `1`, wraps `python -m Server` in a parent that restarts the child after a crash or after repeated failures of `GET http://127.0.0.1:{port}/health`. |
| `WATCHDOG_INTERVAL_SEC` | `15` | Seconds between health checks when supervising. |
| `WATCHDOG_FAIL_THRESHOLD` | `3` | Consecutive failed checks before SIGTERM and restart. |
| `WATCHDOG_STARTUP_GRACE_SEC` | `12` | Ignore failures until this long after each child spawn. |

**`GET /health`** returns `{"status":"ok"}` without touching MySQL (same port as the API, path is always `/health`).

Example (dev with reload + crash/stuck recovery):

```bash
UVICORN_RELOAD=1 SERVER_SUPERVISE=1 python -m Server
```

Production typically uses `UVICORN_RELOAD=0` and relies on systemd or Docker for restarts instead of `SERVER_SUPERVISE`.

---

## Podman (same ports as nginx examples)

Scripts live under **`scripts/podman/`**. They bind only on **loopback** so your host nginx can keep using:

| Upstream | Host bind | Matches |
|----------|-----------|---------|
| API + WebSocket | `127.0.0.1:8000` | `http://127.0.0.1:8000/api/*` and `/ws/...` |
| Web frontend | `127.0.0.1:3000` | Proxy your site root to this if you use the Python frontend |

```bash
./scripts/podman/build.sh
./scripts/podman/run-server.sh    # API_PREFIX=/api, WS_PREFIX=/ws, .env from repo root
./scripts/podman/run-frontend.sh  # optional; AUTH_BACKEND → API container on podman network
# or: ./scripts/podman/run-all.sh
```

**MySQL on the host:** set `DB_HOST=host.containers.internal` in `.env` (the API container adds `host.containers.internal` → host gateway). If you only serve HTML via nginx from disk, you can skip `run-frontend.sh` and still use `run-server.sh` for `:8000`.

**Override ports (must match nginx):** `AUTH_SERVER_PORT=8000 ./scripts/podman/run-server.sh` (default), `FRONTEND_HOST_PORT=3000 ./scripts/podman/run-frontend.sh` (default).

**Stop:** `./scripts/podman/stop.sh`

**After code changes:** rebuild the image and recreate containers (same `.env` and ports):

```bash
./scripts/podman/reload.sh              # API + frontend images and both containers
./scripts/podman/reload.sh server       # only Server/ + Database/ (faster)
./scripts/podman/reload.sh frontend     # only Server/frontend/
```

`run-*.sh` uses `podman run --replace`, so the old container is replaced by one running the newly built image.

**Logs:** Python writes under repo **`logs/`** (gitignored): structured `*_YYYYMMDD.log` from `logging_config`, plus **`server.stdout.log` / `server.stderr.log`** (and **`frontend.*`** for the small web server). The same directory is bind-mounted at **`/app/logs`** in containers (`NETFLOWER_LOG_DIR` overrides the host path). Output still goes to the container’s stdout/stderr so `podman logs` works. Set **`PYTHON_STREAM_TEE=0`** to skip the `.stdout.log` / `.stderr.log` files.
