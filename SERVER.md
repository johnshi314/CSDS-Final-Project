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

```bash
pip install -r requirements.txt
python -m Server
```

At startup the log line shows the mounted HTTP prefix and WebSocket path.
