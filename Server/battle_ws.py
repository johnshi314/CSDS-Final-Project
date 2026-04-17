"""
Battle WebSocket: server-authoritative turn order and timers after lobby.

Protocol (text frames, UTF-8):

Client -> server
  start|<num_agents>|<turn_seconds>     - first message starts config (same from any client; first wins)
  claim|<slot>                           - authenticated player claims turn-order slot (0..n-1)
  claim|<slot>|npc                       - slot is AI; only match host may pass for that slot
  spawns|<n>|<x0>|<y0>|...               - after all claims: lowest-connected player_id (see claims_complete) sends map positions for turn slots 0..n-1 (same order as Unity turnOrder: red0,blue0,red1,...)
  pass                                   - end current turn (must own current slot, or host for NPC slot)
  relay|move|<slot>|<tx>|<ty>            - announce move (legacy slot index; fragile if roster changes)
  relay|ability|<slot>|<ability_index>|<tx>|<ty>
  relay|moveu|<unit_id>|<tx>|<ty>        - move by stable unit id (Unity: r0,r1 / b0,b1 by team list order)
  relay|abilityu|<unit_id>|<ability_index>|<tx>|<ty>
  relay|endturn|<slot>                   - optional explicit end (usually use pass)

  The server does not parse relay payloads; it broadcasts them. Prefer moveu/abilityu so both peers
  resolve the same Agent after kills or reordering when red/blue lists and ordering match.

Server -> client
  you|<player_id>                        - who you are (JWT)
  ack|start|<n>|<t>                      - battle config accepted
  ack|claim|<slot>|<player_id>           - slot assignment
  claims_complete|<host_player_id>       - all slots claimed; host must send spawns|... next
  spawnLayout|<n>|<x0>|<y0>|...          - authoritative start tiles (broadcast after valid spawns)
  battle_ready                           - spawn layout locked; first turn imminent
  newTurn|<slot>|<sync_turn>|<ends_unix> - unix seconds when turn ends (UTC)
  tick|<slot>|<seconds_left_int>         - about 2 Hz while turn active
  relay|<from_player_id>|<payload>       - payload = rest after from_pid (same as client relay line)
  err|<message>                          - non-fatal warning
"""
from __future__ import annotations

import asyncio
import time
from dataclasses import dataclass, field

from fastapi import APIRouter, WebSocket, WebSocketDisconnect

from logging_config import get_logger
from Server.security import get_player_id_from_websocket

logger = get_logger(__name__)
router = APIRouter()

TIMER_TICK_SEC = 0.5


@dataclass
class BattleClient:
    player_id: int
    ws: WebSocket


@dataclass
class BattleRoom:
    match_id: int
    clients: list[BattleClient] = field(default_factory=list)
    lock: asyncio.Lock = field(default_factory=asyncio.Lock)
    num_agents: int = 0
    turn_seconds: float = 30.0
    slot_owner: dict[int, int] = field(default_factory=dict)  # slot -> player_id
    current_slot: int = 0
    sync_turn: int = 0  # matches Unity BattleManager.currentTurn
    battle_ready: bool = False
    awaiting_spawns: bool = False
    battle_loop_task: asyncio.Task | None = None
    turn_deadline_monotonic: float = 0.0
    pass_received: bool = False


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


def _host_player_id(room: BattleRoom) -> int | None:
    if not room.clients:
        return None
    return min(c.player_id for c in room.clients)


def _owner_for_slot(room: BattleRoom, slot: int) -> int | None:
    return room.slot_owner.get(slot)


def _may_act_pass(room: BattleRoom, player_id: int) -> bool:
    slot = room.current_slot
    owner = _owner_for_slot(room, slot)
    if owner is None:
        return False
    if owner < 0:
        return player_id == _host_player_id(room)
    return owner == player_id


def _advance_turn(room: BattleRoom) -> None:
    n = room.num_agents
    if n <= 0:
        return
    if n == 1:
        room.sync_turn += 1
        room.current_slot = 0
        return
    prev = room.current_slot
    room.current_slot = (room.current_slot + 1) % n
    if room.current_slot == 0 and prev != 0:
        room.sync_turn += 1


async def _battle_loop(room: BattleRoom) -> None:
    try:
        while True:
            async with room.lock:
                if not room.clients or room.num_agents <= 0:
                    await asyncio.sleep(0.2)
                    continue
                if not room.battle_ready:
                    await asyncio.sleep(0.1)
                    continue

            room.pass_received = False
            now_m = time.monotonic()
            room.turn_deadline_monotonic = now_m + float(room.turn_seconds)
            ends_unix = time.time() + float(room.turn_seconds)

            slot = room.current_slot
            st = room.sync_turn
            await _broadcast(
                room,
                f"newTurn|{slot}|{st}|{ends_unix:.3f}",
            )

            while True:
                now_m = time.monotonic()
                left = room.turn_deadline_monotonic - now_m
                if room.pass_received:
                    break
                if left <= 0:
                    break
                sec_left = max(0, int(left))
                await _broadcast(room, f"tick|{room.current_slot}|{sec_left}")
                await asyncio.sleep(TIMER_TICK_SEC)

            _advance_turn(room)
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
        if room.battle_loop_task is not None:
            room.battle_loop_task.cancel()
            room.battle_loop_task = None
        _rooms.pop(match_id, None)


def _try_start_battle_loop(room: BattleRoom) -> None:
    if room.battle_loop_task is not None and not room.battle_loop_task.done():
        return
    room.battle_loop_task = asyncio.create_task(_battle_loop(room))


async def _handle_client_message(room: BattleRoom, player_id: int, text: str) -> None:
    # Full split (spawns|n|x|y|... needs unbounded segments; relay still uses raw text below).
    parts = text.split("|")
    head = parts[0].strip().lower() if parts else ""

    if head == "start" and len(parts) >= 3:
        try:
            n = int(parts[1])
            t = float(parts[2])
        except ValueError:
            await _send_err(room, player_id, "bad start line")
            return
        if n < 1 or n > 32 or t < 3 or t > 600:
            await _send_err(room, player_id, "start out of range")
            return
        async with room.lock:
            if room.num_agents == 0:
                room.num_agents = n
                room.turn_seconds = t
        await _broadcast(room, f"ack|start|{room.num_agents}|{int(room.turn_seconds)}")
        return

    if head == "claim" and len(parts) >= 2:
        try:
            slot = int(parts[1])
        except ValueError:
            return
        is_npc = len(parts) >= 3 and parts[2].strip().lower() == "npc"
        async with room.lock:
            if room.num_agents <= 0 or slot < 0 or slot >= room.num_agents:
                await _send_err(room, player_id, "bad slot")
                return
            existing = room.slot_owner.get(slot)
            if existing is not None and existing != player_id and existing != -1:
                return
            # NPC slots use owner -1; only the match host may issue pass for them.
            room.slot_owner[slot] = -1 if is_npc else player_id
        ack_pid = -1 if is_npc else player_id
        await _broadcast(room, f"ack|claim|{slot}|{ack_pid}")
        await _check_all_claimed(room)
        return

    if head == "spawns" and len(parts) >= 3:
        await _handle_spawns_line(room, player_id, parts)
        return

    if head == "pass":
        if not room.battle_ready:
            return
        if not _may_act_pass(room, player_id):
            await _send_err(room, player_id, "not your turn")
            return
        room.pass_received = True
        return

    if head == "relay":
        payload = text[6:] if text.startswith("relay|") else text
        await _broadcast(room, f"relay|{player_id}|{payload}")
        return


async def _send_err(room: BattleRoom, player_id: int, msg: str) -> None:
    for c in room.clients:
        if c.player_id == player_id:
            try:
                await c.ws.send_text(f"err|{msg}")
            except Exception:
                pass
            break


async def _check_all_claimed(room: BattleRoom) -> None:
    async with room.lock:
        if room.awaiting_spawns or room.battle_ready or room.num_agents <= 0:
            return
        if len(room.slot_owner) < room.num_agents:
            return
        for s in range(room.num_agents):
            if s not in room.slot_owner:
                return
        room.awaiting_spawns = True
        room.current_slot = 0
        room.sync_turn = 0
    hp = _host_player_id(room) or 0
    await _broadcast(room, f"claims_complete|{hp}")


async def _handle_spawns_line(room: BattleRoom, player_id: int, parts: list[str]) -> None:
    hp = _host_player_id(room)
    if hp is None or player_id != hp:
        await _send_err(room, player_id, "only spawn host may send spawns")
        return
    try:
        n = int(parts[1])
    except (ValueError, IndexError):
        await _send_err(room, player_id, "bad spawns n")
        return
    if n < 1 or n > 32 or n != room.num_agents:
        await _send_err(room, player_id, "spawn count mismatch")
        return
    if len(parts) != 2 + 2 * n:
        await _send_err(room, player_id, "bad spawns arity")
        return
    coords: list[tuple[int, int]] = []
    for i in range(n):
        try:
            x = int(parts[2 + 2 * i])
            y = int(parts[3 + 2 * i])
        except ValueError:
            await _send_err(room, player_id, "bad spawn coordinate")
            return
        coords.append((x, y))
    if len(set(coords)) != len(coords):
        await _send_err(room, player_id, "spawn positions not unique")
        return

    async with room.lock:
        if not room.awaiting_spawns or room.battle_ready:
            await _send_err(room, player_id, "spawns not expected now")
            return
        room.awaiting_spawns = False
        room.battle_ready = True

    flat = [str(n)] + [str(v) for xy in coords for v in xy]
    await _broadcast(room, "spawnLayout|" + "|".join(flat))
    await _broadcast(room, "battle_ready")
    _try_start_battle_loop(room)


@router.websocket("/battle/{match_id}")
async def battle_websocket(websocket: WebSocket, match_id: int):
    if match_id <= 0:
        await websocket.close(code=4000)
        return
    auth_pid = get_player_id_from_websocket(websocket)
    if auth_pid is None:
        await websocket.close(code=4003)
        return

    await websocket.accept()
    room = await _get_or_create_room(match_id)
    client = BattleClient(player_id=auth_pid, ws=websocket)
    async with room.lock:
        room.clients = [c for c in room.clients if c.player_id != auth_pid]
        room.clients.append(client)
    _try_start_battle_loop(room)

    logger.info("Battle ws connect: match=%s player=%s", match_id, auth_pid)
    try:
        await websocket.send_text(f"you|{auth_pid}")
    except Exception:
        pass

    try:
        while True:
            message = await websocket.receive_text()
            await _handle_client_message(room, auth_pid, message)
    except WebSocketDisconnect:
        pass
    finally:
        async with room.lock:
            room.clients = [c for c in room.clients if c.player_id != auth_pid]
        logger.info("Battle ws disconnect: match=%s player=%s", match_id, auth_pid)
        await _maybe_cleanup_room(match_id)
