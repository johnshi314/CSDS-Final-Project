import asyncio
import json

from fastapi import WebSocket

from Database import queries
from logging_config import get_logger

logger = get_logger(__name__)

lobby_connections: dict[int, list[WebSocket]] = {}
_lobby_conn_lock = asyncio.Lock()


async def register_lobby_ws(match_id: int, ws: WebSocket) -> None:
    async with _lobby_conn_lock:
        conns = lobby_connections.setdefault(match_id, [])
        conns.append(ws)
        logger.info("Lobby runtime register match=%s subscribers=%s", match_id, len(conns))


async def unregister_lobby_ws(match_id: int, ws: WebSocket) -> None:
    async with _lobby_conn_lock:
        conns = lobby_connections.get(match_id)
        if not conns:
            return
        lobby_connections[match_id] = [c for c in conns if c is not ws]
        if not lobby_connections[match_id]:
            del lobby_connections[match_id]
            logger.info("Lobby runtime unregister match=%s subscribers=0", match_id)
        else:
            logger.info(
                "Lobby runtime unregister match=%s subscribers=%s",
                match_id,
                len(lobby_connections[match_id]),
            )


async def broadcast_lobby_snapshot(match_id: int) -> None:
    """Push current lobby JSON to every client subscribed to this match."""
    try:
        snap = queries.get_lobby_snapshot(match_id)
        body = json.dumps(snap)
    except Exception:
        logger.exception("get_lobby_snapshot in broadcast")
        return
    async with _lobby_conn_lock:
        conns = list(lobby_connections.get(match_id, []))
    logger.info("Lobby runtime broadcast match=%s subscribers=%s", match_id, len(conns))
    dead: list[WebSocket] = []
    for ws in conns:
        try:
            await ws.send_text(body)
        except Exception:
            dead.append(ws)
    if dead:
        async with _lobby_conn_lock:
            cur = lobby_connections.get(match_id, [])
            lobby_connections[match_id] = [c for c in cur if c not in dead]
            if not lobby_connections[match_id]:
                del lobby_connections[match_id]
