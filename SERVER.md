# WebSocket Turn-Based Server

Python WebSocket server for Unity desktop clients. Multiple clients connect; the server runs round-robin turns with a 2-second pause per player, relays each player’s message to everyone, and sends a epoch counter between turns.

## Protocol (pipe-separated)

**Server > clients**

- `you|playerId` — sent once on connect; this client’s player id (0, 1, 2, …).
- `turn|playerId` — it’s this player’s turn (they can send one message).
- `said|playerId|message` — that player’s message this turn (relayed to all).
- `epoch|n` — server epoch counter (increments after each player’s turn), or epoch.

**Client > server**

- Plain text — only accepted when it’s that client’s turn; otherwise ignored.

## Setup

From the **project root**:

```bash
pip install -r Server/requirements.txt
```

## Run

From the **project root**:

```bash
python -m Server
```

Server listens on **ws://0.0.0.0:8765**. Unity `WebSocketClient` uses **ws://localhost:8765** by default.

## Test

1. Start the server: `python -m Server`
2. Open two or more Unity instances (or builds), each with `WebSocketClient` on a GameObject.
3. Enter Play in each. Each client gets `you|id`, then in order: `turn|0` (2s), `said|0|...`, `epoch|1`, `turn|1` (2s), `said|1|...`, `epoch|2`, …
4. Clients only send when it’s their turn; they listen for `said` and `epoch` in the Console.
