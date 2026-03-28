##############################################################
# File: Server/multiplayer_echo.py
# Author: Mikey Maldonado
# Description: Turn-based WebSocket server for Unity clients.
# Date Created: 2026-01-31
##############################################################
"""
Turn-based WebSocket server for Unity clients.
- Multiple clients connect; each gets a player id (you|id).
- Server runs round-robin: for each client, announces turn|playerId, waits 2s,
  relays that client's message to everyone (said|playerId|message), then sends
  an epoch counter (epoch|n) to all. Clients wait for their turn and listen for
  said/epoch messages.
"""
import os
import asyncio
from websockets.asyncio.server import serve
from logging_config import get_logger

logger = get_logger(__name__)

SERVER_HOST = os.getenv("SERVER_HOST", "0.0.0.0")
SERVER_PORT = int(os.getenv("SERVER_PORT", 8765))

# Shared state: modified by connection handlers and game loop.
# Use "global name" in a function only when you *assign* to that name
# (e.g. clients = ..., next_player_id += 1). Reading or mutating in place
# (e.g. clients.append(...), turn_message[pid] = x) does not need global.
clients = []            # list of {"id": int, "ws": websocket}
turn_message = {}       # client_id -> str (message they sent this turn)
current_turn_id = None  # whose turn it is (for accepting messages)
epoch_counter = 0       # number of completed epochs
next_player_id = 0      # assign new player ids incrementally


async def broadcast(msg: str):
    """Send message to all connected clients. (Only reads clients, no global needed.)"""
    dead = []
    for c in clients:
        try:
            await c["ws"].send(msg)
        except Exception:
            dead.append(c)
    for c in dead:
        await remove_client(c["id"])
        logger.info("Client %s disconnected (broadcast failed)", c["id"])

async def remove_client(pid: int):
    """Remove client by player id."""
    global clients
    clients = [c for c in clients if c["id"] != pid]
    if pid in turn_message:
        turn_message.pop(pid)
    logger.info("Client %s removed", pid)

async def game_loop():
    """Round-robin: turn -> 2s pause -> relay said -> epoch -> next player."""
    global current_turn_id, epoch_counter  # we assign to these; clients only read
    while True:
        if not clients:
            await asyncio.sleep(1)
            continue
        for c in list(clients):
            pid = c["id"]
            current_turn_id = pid
            turn_message[pid] = ""
            await broadcast(f"turn|{pid}")
            logger.info("Turn: player %s", pid)
            await asyncio.sleep(2)
            msg = turn_message.get(pid, "") or ""
            await broadcast(f"said|{pid}|{msg}")
            epoch_counter += 1
            await broadcast(f"epoch|{epoch_counter}")
            current_turn_id = None
            if not clients:
                break
        await asyncio.sleep(0.1)


async def handler(websocket):
    """Per-client: assign id, send you|id, then accept messages only on their turn."""
    global next_player_id
    pid = next_player_id
    next_player_id += 1
    clients.append({"id": pid, "ws": websocket})
    remote = websocket.remote_address
    logger.info("Client connected: %s -> player %s", remote, pid)
    try:
        await websocket.send(f"you|{pid}")
        async for message in websocket:
            if current_turn_id == pid:
                turn_message[pid] = message
                logger.debug("Player %s said: %s", pid, message)
    except Exception as e:
        logger.info("Client %s error: %s", pid, e)
    finally:
        await remove_client(pid)
        logger.info("Client disconnected: player %s", pid)


async def main(host: str = SERVER_HOST, port: int = SERVER_PORT):
    """Run the turn-based server."""
    logger.info("Turn-based WebSocket server on ws://%s:%s", host, port)
    async with serve(handler, host, port):
        asyncio.create_task(game_loop())
        try:
            await asyncio.Future()
        except asyncio.CancelledError:
            logger.info("WebSocket server shutting down...")


if __name__ == "__main__":
    from logging_config import install_stream_tee

    install_stream_tee("multiplayer_echo")
    try:
        asyncio.run(main(host=SERVER_HOST, port=SERVER_PORT))
    except KeyboardInterrupt:
        pass  # Graceful shutdown on Ctrl+C
