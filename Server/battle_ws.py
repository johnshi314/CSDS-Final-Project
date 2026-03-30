import asyncio
from dataclasses import dataclass, field

from fastapi import APIRouter, WebSocket, WebSocketDisconnect

from logging_config import get_logger
from Server.security import get_player_id_from_websocket

logger = get_logger(__name__)
router = APIRouter()


@dataclass
class BattleClient:
    player_id: int
    ws: WebSocket


@dataclass
class BattleRoom:
    match_id: int
    clients: list[BattleClient] = field(default_factory=list)
    turn_message: dict[int, str] = field(default_factory=dict)
    current_turn_id: int | None = None
    epoch_counter: int = 0
    loop_task: asyncio.Task | None = None
    lock: asyncio.Lock = field(default_factory=asyncio.Lock)


_rooms: dict[int, BattleRoom] = {}
_rooms_lock = asyncio.Lock()


async def _broadcast(room: BattleRoom, msg: str) -> None:
    dead: list[int] = []
    for c in list(room.clients):
        try:
            await c.ws.send_text(msg)
        except Exception:
            dead.append(c.player_id)
    if dead:
        async with room.lock:
            room.clients = [c for c in room.clients if c.player_id not in dead]
            for pid in dead:
                room.turn_message.pop(pid, None)


async def _game_loop(room: BattleRoom) -> None:
    """Inspired by multiplayer_echo: turn -> relay said -> epoch."""
    try:
        while True:
            if not room.clients:
                await asyncio.sleep(0.2)
                continue
            for c in list(room.clients):
                pid = c.player_id
                room.current_turn_id = pid
                room.turn_message[pid] = ""
                await _broadcast(room, f"turn|{pid}")
                await asyncio.sleep(2)
                said = room.turn_message.get(pid, "") or ""
                await _broadcast(room, f"said|{pid}|{said}")
                room.epoch_counter += 1
                await _broadcast(room, f"epoch|{room.epoch_counter}")
                room.current_turn_id = None
                if not room.clients:
                    break
            await asyncio.sleep(0.05)
    except asyncio.CancelledError:
        logger.info("Battle loop cancelled for match %s", room.match_id)
        raise


async def _get_or_create_room(match_id: int) -> BattleRoom:
    async with _rooms_lock:
        room = _rooms.get(match_id)
        if room is None:
            room = BattleRoom(match_id=match_id)
            _rooms[match_id] = room
        return room


async def _maybe_cleanup_room(match_id: int) -> None:
    async with _rooms_lock:
        room = _rooms.get(match_id)
        if room is None:
            return
        if room.clients:
            return
        if room.loop_task is not None:
            room.loop_task.cancel()
        _rooms.pop(match_id, None)


@router.websocket("/battle/{match_id}")
async def battle_websocket(websocket: WebSocket, match_id: int):
    """
    Authenticated battle websocket on the same FastAPI app.

    - Auth via ?authToken=... or auth_token cookie
    - Emits: you|<playerId>, turn|<playerId>, said|<playerId>|<msg>, epoch|<n>
    """
    if match_id <= 0:
        await websocket.close(code=4000)
        return
    player_id = get_player_id_from_websocket(websocket)
    if player_id is None:
        await websocket.close(code=4003)
        return

    await websocket.accept()
    room = await _get_or_create_room(match_id)
    async with room.lock:
        room.clients.append(BattleClient(player_id=player_id, ws=websocket))
        if room.loop_task is None or room.loop_task.done():
            room.loop_task = asyncio.create_task(_game_loop(room))

    logger.info("Battle ws connect: match=%s player=%s", match_id, player_id)
    await websocket.send_text(f"you|{player_id}")

    try:
        while True:
            message = await websocket.receive_text()
            if room.current_turn_id == player_id:
                room.turn_message[player_id] = message
    except WebSocketDisconnect:
        pass
    finally:
        async with room.lock:
            room.clients = [c for c in room.clients if c.player_id != player_id]
            room.turn_message.pop(player_id, None)
            room.current_turn_id = None if room.current_turn_id == player_id else room.current_turn_id
        logger.info("Battle ws disconnect: match=%s player=%s", match_id, player_id)
        await _maybe_cleanup_room(match_id)
