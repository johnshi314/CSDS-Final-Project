import asyncio
import json

from fastapi import APIRouter, WebSocket, WebSocketDisconnect

from Database import queries
from logging_config import get_logger
from Server.lobby_runtime import (
    broadcast_lobby_snapshot,
    register_lobby_ws,
    unregister_lobby_ws,
)
from Server.security import get_player_id_from_websocket

logger = get_logger(__name__)
router = APIRouter()

LOBBY_WS_HEARTBEAT_SEC = 30


def _peer(websocket: WebSocket) -> str:
    client = websocket.client
    if client is None:
        return "unknown"
    return f"{client.host}:{client.port}"


@router.websocket("/lobby/{match_id}")
async def lobby_websocket(websocket: WebSocket, match_id: int):
    """
    Backward-compatible lobby feed endpoint.
    Auth via ?authToken=... or auth_token cookie.
    """
    if match_id <= 0:
        logger.info("Reject lobby ws peer=%s match=%s reason=invalid_match", _peer(websocket), match_id)
        await websocket.close(code=4000)
        return
    player_id = get_player_id_from_websocket(websocket)
    if player_id is None:
        logger.info("Reject lobby ws peer=%s match=%s reason=unauthenticated", _peer(websocket), match_id)
        await websocket.close(code=4003)
        return
    if not queries.is_player_in_lobby(match_id, player_id):
        logger.info(
            "Reject lobby ws peer=%s player=%s match=%s reason=not_in_lobby",
            _peer(websocket),
            player_id,
            match_id,
        )
        await websocket.close(code=4001)
        return
    await websocket.accept()
    logger.info("Lobby ws connected peer=%s player=%s match=%s", _peer(websocket), player_id, match_id)
    await register_lobby_ws(match_id, websocket)
    try:
        await websocket.send_text(json.dumps(queries.get_lobby_snapshot(match_id)))
        while True:
            try:
                msg = await asyncio.wait_for(
                    websocket.receive(), timeout=LOBBY_WS_HEARTBEAT_SEC
                )
            except asyncio.TimeoutError:
                try:
                    await websocket.send_text('{"heartbeat":true}')
                except Exception:
                    break
                continue
            if msg.get("type") == "websocket.disconnect":
                break
    except WebSocketDisconnect:
        pass
    finally:
        logger.info("Lobby ws disconnect peer=%s player=%s match=%s", _peer(websocket), player_id, match_id)
        await unregister_lobby_ws(match_id, websocket)
        status = queries.get_lobby_status(match_id)
        if status == "lobby":
            queries.remove_lobby_player(match_id, player_id)
            logger.info("Player %s left lobby %s (disconnect)", player_id, match_id)
            if queries.lobby_is_empty(match_id):
                queries.mark_match_lobby_completed(match_id)
                logger.info("Lobby %s is now empty - marked completed", match_id)
            else:
                await broadcast_lobby_snapshot(match_id)


@router.websocket("/lobby-control")
async def lobby_control_websocket(websocket: WebSocket):
    """
    New stateful lobby control websocket.

    Client messages (JSON):
    - {"action":"joinNewLobby", "maxPlayers":8}
    - {"action":"subscribeLobby", "matchId":123}
    - {"action":"setTeam", "team":"red"}
    - {"action":"setReady"}
    - {"action":"leaveLobby"}
    - {"action":"snapshot"}
    """
    player_id = get_player_id_from_websocket(websocket)
    if player_id is None:
        logger.info("Reject lobby-control ws peer=%s reason=unauthenticated", _peer(websocket))
        await websocket.close(code=4003)
        return

    current_match_id: int | None = None
    await websocket.accept()
    logger.info("Lobby-control ws connected peer=%s player=%s", _peer(websocket), player_id)

    async def send_error(detail: str):
        logger.info(
            "Lobby-control error peer=%s player=%s match=%s detail=%s",
            _peer(websocket),
            player_id,
            current_match_id,
            detail,
        )
        await websocket.send_text(json.dumps({"type": "error", "detail": detail}))

    async def subscribe(mid: int):
        nonlocal current_match_id
        if current_match_id == mid:
            return
        if current_match_id is not None:
            await unregister_lobby_ws(current_match_id, websocket)
        await register_lobby_ws(mid, websocket)
        current_match_id = mid
        logger.info("Lobby-control subscribed peer=%s player=%s match=%s", _peer(websocket), player_id, mid)

    try:
        await websocket.send_text(json.dumps({"type": "connected", "playerId": player_id}))
        while True:
            raw = await websocket.receive_text()
            try:
                msg = json.loads(raw)
            except json.JSONDecodeError:
                await send_error("Invalid JSON")
                continue

            action = msg.get("action")
            logger.info(
                "Lobby-control action peer=%s player=%s match=%s action=%s",
                _peer(websocket),
                player_id,
                current_match_id,
                action,
            )
            if action == "joinNewLobby":
                max_players = int(msg.get("maxPlayers", 8))
                mid = queries.join_new_lobby(player_id, max_players=max_players)
                if mid is None:
                    await send_error("Could not join or create lobby")
                    continue
                await subscribe(mid)
                await websocket.send_text(json.dumps({"type": "joinedLobby", "matchId": mid}))
                await broadcast_lobby_snapshot(mid)
                continue

            if action == "subscribeLobby":
                mid = int(msg.get("matchId", 0))
                if mid <= 0:
                    await send_error("Invalid matchId")
                    continue
                if not queries.is_player_in_lobby(mid, player_id):
                    await send_error("Player is not part of this lobby")
                    continue
                await subscribe(mid)
                await websocket.send_text(json.dumps({"type": "subscribed", "matchId": mid}))
                await websocket.send_text(json.dumps(queries.get_lobby_snapshot(mid)))
                continue

            if action == "setTeam":
                if current_match_id is None:
                    await send_error("Not subscribed to a lobby")
                    continue
                team = (msg.get("team") or "").strip().lower()
                result = queries.set_lobby_team(current_match_id, player_id, team)
                if isinstance(result, str):
                    await send_error(result)
                    continue
                if not result:
                    await send_error("Invalid team or player not in this lobby")
                    continue
                await broadcast_lobby_snapshot(current_match_id)
                continue

            if action == "setReady":
                if current_match_id is None:
                    await send_error("Not subscribed to a lobby")
                    continue
                result = queries.set_lobby_ready(current_match_id, player_id, True)
                if isinstance(result, str):
                    await send_error(result)
                    continue
                if not result:
                    await send_error("Player not in this lobby")
                    continue
                snap = queries.get_lobby_snapshot(current_match_id)
                if snap and snap.get("everyoneReady"):
                    queries.mark_match_lobby_in_progress(current_match_id)
                await broadcast_lobby_snapshot(current_match_id)
                continue

            if action == "leaveLobby":
                if current_match_id is None:
                    await send_error("Not subscribed to a lobby")
                    continue
                status = queries.get_lobby_status(current_match_id)
                if status and status != "lobby":
                    await send_error(f"Lobby is {status}")
                    continue
                removed = queries.remove_lobby_player(current_match_id, player_id)
                if not removed:
                    await send_error("Player not in this lobby")
                    continue
                await unregister_lobby_ws(current_match_id, websocket)
                left_mid = current_match_id
                current_match_id = None
                if queries.lobby_is_empty(left_mid):
                    queries.mark_match_lobby_completed(left_mid)
                else:
                    await broadcast_lobby_snapshot(left_mid)
                await websocket.send_text(json.dumps({"type": "leftLobby", "matchId": left_mid}))
                continue

            if action == "snapshot":
                if current_match_id is None:
                    await send_error("Not subscribed to a lobby")
                    continue
                await websocket.send_text(json.dumps(queries.get_lobby_snapshot(current_match_id)))
                continue

            await send_error("Unknown action")

    except WebSocketDisconnect:
        pass
    finally:
        logger.info(
            "Lobby-control ws disconnect peer=%s player=%s match=%s",
            _peer(websocket),
            player_id,
            current_match_id,
        )
        if current_match_id is not None:
            await unregister_lobby_ws(current_match_id, websocket)
