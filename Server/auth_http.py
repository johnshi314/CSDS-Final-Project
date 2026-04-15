"""
HTTP Auth Server for Unity Game
Provides REST endpoints for registration, login, recordkeeping, and token verification.
"""
from datetime import datetime, timedelta, timezone
import os
from contextlib import asynccontextmanager

import bcrypt
import jwt
import asyncio
import json

from fastapi import (
    APIRouter,
    Depends,
    FastAPI,
    HTTPException,
    Query,
    Request,
    Response,
    Security,
    WebSocket,
    WebSocketDisconnect,
)
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer
from pydantic import BaseModel, Field
from sqlalchemy.exc import IntegrityError

from Database import queries
from logging_config import get_logger

logger = get_logger(__name__)

# Auth Server Configuration
AUTH_SERVER_HOST = os.getenv("AUTH_SERVER_HOST", "0.0.0.0")
AUTH_SERVER_PORT = int(os.getenv("AUTH_SERVER_PORT", 8000))

# Reverse-proxy path prefixes (e.g. nginx: /api/* and /ws/* -> this app)
def _norm_prefix(p: str) -> str:
    p = (p or "").strip()
    if not p:
        return ""
    return "/" + p.strip("/")


API_PREFIX = _norm_prefix(os.getenv("API_PREFIX", ""))
WS_PREFIX = _norm_prefix(os.getenv("WS_PREFIX", "/ws")) or "/ws"

# JWT Configuration
JWT_SECRET = os.getenv('JWT_SECRET', 'change_this_secret_key')
JWT_ALGORITHM = os.getenv('JWT_ALGORITHM', 'HS256')
JWT_EXPIRATION_HOURS = int(os.getenv('JWT_EXPIRATION_HOURS', 24))


@asynccontextmanager
async def lifespan(app: FastAPI):
    # No table initialization needed - using existing schema
    logger.info("Auth server started")
    yield
    logger.info("Auth server shutdown")


app = FastAPI(title="Unity Auth Server", lifespan=lifespan)
api_router = APIRouter()
ws_router = APIRouter()


@app.get("/health")
def health():
    """Liveness probe; does not touch the database. Use for watchdogs and load balancers."""
    return {"status": "ok"}


# --- Lobby WebSocket registry (same process as HTTP; use this for live pushes) ---
lobby_connections: dict[int, list[WebSocket]] = {}
_lobby_conn_lock = asyncio.Lock()


async def _register_lobby_ws(match_id: int, ws: WebSocket) -> None:
    async with _lobby_conn_lock:
        lobby_connections.setdefault(match_id, []).append(ws)


async def _unregister_lobby_ws(match_id: int, ws: WebSocket) -> None:
    async with _lobby_conn_lock:
        conns = lobby_connections.get(match_id)
        if not conns:
            return
        lobby_connections[match_id] = [c for c in conns if c is not ws]
        if not lobby_connections[match_id]:
            del lobby_connections[match_id]


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


class RegisterRequest(BaseModel):
    password: str = Field(min_length=8)
    username: str = Field(min_length=1, max_length=255)


class LoginRequest(BaseModel):
    username: str
    password: str


class TokenVerifyRequest(BaseModel):
    authToken: str


def _normalize_username(name: str) -> str:
    return (name or "").strip()


def generate_jwt_token(player_id: int) -> str:
    payload = {
        'player_id': player_id,
        'exp': datetime.now(timezone.utc) + timedelta(hours=JWT_EXPIRATION_HOURS),
        'iat': datetime.now(timezone.utc)
    }
    return jwt.encode(payload, JWT_SECRET, algorithm=JWT_ALGORITHM)


def verify_jwt_token(token: str):
    try:
        return jwt.decode(token, JWT_SECRET, algorithms=[JWT_ALGORITHM])
    except jwt.ExpiredSignatureError:
        return None
    except jwt.InvalidTokenError:
        return None


_bearer_scheme = HTTPBearer(auto_error=False)


def get_current_player(
    request: Request,
    credentials: HTTPAuthorizationCredentials | None = Security(_bearer_scheme),
) -> int:
    """Resolve authenticated player_id from Bearer header or auth_token cookie."""
    token = credentials.credentials if credentials else None
    if not token:
        token = request.cookies.get("auth_token")
    if not token:
        raise HTTPException(status_code=401, detail="Authentication required")
    payload = verify_jwt_token(token)
    if payload is None:
        raise HTTPException(status_code=401, detail="Invalid or expired token")
    return payload["player_id"]


def _ws_authenticate(websocket: WebSocket) -> int | None:
    """Extract player_id from a WebSocket's ?token= query param or auth_token cookie."""
    token = websocket.query_params.get("token")
    if not token:
        token = websocket.cookies.get("auth_token")
    if not token:
        return None
    payload = verify_jwt_token(token)
    return payload["player_id"] if payload else None


@api_router.post("/register")
def register(payload: RegisterRequest, response: Response):
    password = payload.password
    username = _normalize_username(payload.username)
    if not username:
        raise HTTPException(status_code=400, detail="Username is required")

    password_hash = bcrypt.hashpw(password.encode("utf-8"), bcrypt.gensalt())

    try:
        player_id = queries.create_player(password_hash.decode("utf-8"), username)
    except IntegrityError:
        raise HTTPException(
            status_code=409, detail="That username is already taken"
        ) from None

    if not isinstance(player_id, int) or player_id <= 0:
        logger.error("create_player returned invalid id: %r", player_id)
        raise HTTPException(status_code=500, detail="Registration failed")

    token = generate_jwt_token(player_id)

    response.set_cookie(
        key="auth_token",
        value=token,
        httponly=True,
        secure=False,
        samesite="lax",
        max_age=JWT_EXPIRATION_HOURS * 3600
    )

    # Unity JsonUtility expects camelCase field names.
    return {
        "status": "success",
        "message": "Registration successful",
        "playerId": player_id,
        "authToken": token,
        "username": username,
    }


@api_router.post("/login")
def login(payload: LoginRequest, request: Request, response: Response):
    player_username = _normalize_username(payload.username)
    password = payload.password

    if not player_username or not password:
        raise HTTPException(status_code=400, detail="Username and password required")

    player = queries.get_player_by_username(player_username)
    if not player:
        raise HTTPException(status_code=401, detail="Invalid player username or password")

    password_match = bcrypt.checkpw(
        password.encode('utf-8'),
        player['hashedpw'].encode('utf-8')
    )

    if not password_match:
        raise HTTPException(status_code=401, detail="Invalid player username or password")

    player_id = player['player_id']
    token = generate_jwt_token(player_id)

    response.set_cookie(
        key="auth_token",
        value=token,
        httponly=True,
        secure=False,
        samesite="lax",
        max_age=JWT_EXPIRATION_HOURS * 3600
    )

    return {
        "status": "success",
        "message": "Login successful",
        "playerId": player_id,
        "authToken": token,
        "username": player_username,
    }


@api_router.post("/verify")
def verify_token(payload: TokenVerifyRequest):
    token = payload.authToken
    verified = verify_jwt_token(token)

    if not verified:
        return {
            "status": "error",
            "valid": False,
            "message": "Invalid or expired token"
        }

    return {
        "status": "success",
        "valid": True,
        "playerId": verified["player_id"],
    }

@api_router.post("/submit-playermatchstats", dependencies=[Depends(get_current_player)])
def submit_playermatchstats(stat: dict):
    """
    Accepts a PlayerMatchStats JSON object from Unity
    and inserts it into the database
    """
    try:
        converted_row = {
            # remove match_player_id for AUTO_INCREMENT
            "match_player_id": None,
            "match_id": stat["matchId"],
            "player_id": stat["playerId"],
            "character_id": stat["characterId"],
            "team_id": stat["teamId"],
            "damage_dealt": stat["damageDealt"],
            "damage_taken": stat["damageTaken"],
            "turns_taken": stat["turnsTaken"],
            "won": stat["won"],
            "disconnected": stat["disconnected"]
        }
        json_string = json.dumps([converted_row])
        queries.insert_match_players(json_string)

        return {
            "status": "success",
            "message": "Player Match stat inserted successfully"
        }

    except KeyError as e:
        raise HTTPException(status_code=400, detail=f"Missing field: {e}")
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
   
@api_router.post("/submit-matchupstats", dependencies=[Depends(get_current_player)])
async def submit_matchupstats(matchup: dict):
    """
    Accepts a MatchStats JSON object from Unity and inserts into database.
    """
    try:

        row = {
            "matchup_id": None,
            "match_id": matchup["matchId"],
            "character_a_id": matchup["characterAId"],
            "character_b_id": matchup["characterBId"],
            "winner_character_id": matchup["winnerCharacterId"]
        }

        queries.insert_matchups(json.dumps([row]))
        return {"status": "success", "message": "MatchupStats inserted successfully"}

    except KeyError as e:
        raise HTTPException(status_code=400, detail=f"Missing field: {e}")
    except Exception as e:
        logger.exception("Submit abilitystats failed")
        raise HTTPException(status_code=500, detail=str(e))

@api_router.post("/submit-abilityusagestats", dependencies=[Depends(get_current_player)])
async def submit_abilityusagestats(ability: dict):
    """
    Accepts an AbilityUsageStats JSON object from Unity and inserts into database.
    """
    try:

        row = {
            "ability_usage_id": None,
            "character_id": ability["characterId"],
            "player_id": ability["playerId"],
            "damage_done": ability["damageDone"],
            "downtime": ability["downtime"],
            "ability_name" : ability["abilityName"]
        }

        queries.insert_ability_usage(json.dumps([row]))
        return {"status": "success", "message": "AbilityStats inserted successfully"}

    except KeyError as e:
        raise HTTPException(status_code=400, detail=f"Missing field: {e}")
    except Exception as e:
        logger.exception("Submit abilitystats failed")
        raise HTTPException(status_code=500, detail=str(e))

@api_router.post("/create-match", dependencies=[Depends(get_current_player)])
def create_match():
    """
    Creates a new match at the beginning of the game
    and returns the generated match_id.
    """
    try:
        match_id = queries.create_match()

        if match_id is None:
            raise HTTPException(status_code=500, detail="Failed to create match")

        return {
            "status": "success",
            "match_id": match_id
        }

    except Exception as e:
        logger.exception("Create match failed")
        raise HTTPException(status_code=500, detail=str(e))

@api_router.api_route("/join-new-lobby", methods=["GET", "POST"])
async def join_new_lobby_endpoint(
    player_id: int = Depends(get_current_player),
    max_players: int = Query(8),
):
    """
    Assign the logged-in player to an open lobby match (or create one).
    Requires authentication; player_id is derived from the JWT token.
    """
    try:
        mid = queries.join_new_lobby(player_id, max_players=max_players)
    except Exception as e:
        logger.exception("join_new_lobby")
        raise HTTPException(
            status_code=503,
            detail=(
                "Lobby database error (usually missing migration). On the MySQL server run "
                "Database/migrations/001_lobby.sql then restart the API. Underlying error: "
                f"{type(e).__name__}: {e}"
            ),
        ) from e
    if mid is None:
        raise HTTPException(
            status_code=503,
            detail="Could not create a lobby match row (create_match returned None). Check MySQL and .env DB_* settings.",
        )
    try:
        await broadcast_lobby_snapshot(mid)
    except Exception:
        logger.exception("broadcast_lobby_snapshot after join (non-fatal)")
    return {"status": "ok", "match_id": mid}


@api_router.get("/get-lobby-updates", dependencies=[Depends(get_current_player)])
def get_lobby_updates(match_id: int):
    """Polling fallback: current lobby snapshot as JSON (same shape as WebSocket pushes)."""
    if match_id <= 0:
        raise HTTPException(status_code=400, detail="Invalid match_id")
    return queries.get_lobby_snapshot(match_id)


@api_router.post("/leave-lobby")
async def leave_lobby_endpoint(
    match_id: int = Query(...),
    player_id: int = Depends(get_current_player),
):
    """Remove the authenticated player from a lobby. No-op if the match is already in progress."""
    status = queries.get_lobby_status(match_id)
    if status and status != "lobby":
        raise HTTPException(status_code=409, detail=f"Lobby is {status}")
    removed = queries.remove_lobby_player(match_id, player_id)
    if not removed:
        raise HTTPException(status_code=400, detail="Player not in this lobby")
    logger.info("Player %s left lobby %s (HTTP)", player_id, match_id)
    if queries.lobby_is_empty(match_id):
        queries.mark_match_lobby_completed(match_id)
    else:
        await broadcast_lobby_snapshot(match_id)
    return {"status": "ok", "match_id": match_id}


@api_router.post("/set-player-team")
async def set_player_team_endpoint(
    match_id: int = Query(...),
    team: str = Query(...),
    player_id: int = Depends(get_current_player),
):
    result = queries.set_lobby_team(match_id, player_id, team)
    if isinstance(result, str):
        raise HTTPException(status_code=409, detail=result)
    if not result:
        raise HTTPException(status_code=400, detail="Invalid team or player not in this lobby")
    await broadcast_lobby_snapshot(match_id)
    return {"status": "ok", "match_id": match_id}


@api_router.get("/set-ready")
async def set_ready_endpoint(match_id: int, player_id: int = Depends(get_current_player)):
    if match_id <= 0:
        raise HTTPException(status_code=400, detail="Invalid match_id")
    result = queries.set_lobby_ready(match_id, player_id, True)
    if isinstance(result, str):
        raise HTTPException(status_code=409, detail=result)
    if not result:
        raise HTTPException(status_code=400, detail="Player not in this lobby")
    snap = queries.get_lobby_snapshot(match_id)
    if snap.get("everyoneReady"):
        queries.mark_match_lobby_in_progress(match_id)
    await broadcast_lobby_snapshot(match_id)
    return {"status": "ok", "match_id": match_id}


LOBBY_WS_HEARTBEAT_SEC = 30

@ws_router.websocket("/lobby/{match_id}")
async def lobby_websocket(websocket: WebSocket, match_id: int):
    """
    Live lobby updates for all players in the same match_id.
    Requires a valid JWT via ?token= query param or auth_token cookie.
    First message after connect is the current snapshot; later pushes mirror HTTP mutations.
    Sends a heartbeat ping every LOBBY_WS_HEARTBEAT_SEC so crashed clients are detected quickly.
    """
    if match_id <= 0:
        await websocket.close(code=4000)
        return
    player_id = _ws_authenticate(websocket)
    if player_id is None:
        await websocket.close(code=4003)
        return
    if not queries.is_player_in_lobby(match_id, player_id):
        await websocket.close(code=4001)
        return
    await websocket.accept()
    await _register_lobby_ws(match_id, websocket)
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
        await _unregister_lobby_ws(match_id, websocket)
        status = queries.get_lobby_status(match_id)
        if status == "lobby":
            queries.remove_lobby_player(match_id, player_id)
            logger.info("Player %s left lobby %s (disconnect)", player_id, match_id)
            if queries.lobby_is_empty(match_id):
                queries.mark_match_lobby_completed(match_id)
                logger.info("Lobby %s is now empty; marked completed", match_id)
            else:
                await broadcast_lobby_snapshot(match_id)


@api_router.post("/update-match", dependencies=[Depends(get_current_player)])
def update_match(match: dict):
    """
    Updates match stats when the game ends.
    """
    try:
        queries.update_match(
            match_id=match["matchId"],
            end_time=match["endTime"],
            duration=match["duration"],
            winner_team_id=match["winnerTeamId"]
        )

        return {
            "status": "success",
            "message": "Match updated successfully"
        }

    except KeyError as e:
        raise HTTPException(status_code=400, detail=f"Missing field: {e}")

    except Exception as e:
        logger.exception("Update match failed")
        raise HTTPException(status_code=500, detail=str(e))


app.include_router(api_router, prefix=API_PREFIX)
# If routes are at root, also mount under /api so nginx can proxy https://host/api/* unchanged.
if not API_PREFIX:
    app.include_router(api_router, prefix="/api")
app.include_router(ws_router, prefix=WS_PREFIX)
logger.info(
    "HTTP API mounted at %r%s; lobby WebSocket at %r",
    API_PREFIX or "/",
    " and /api" if not API_PREFIX else "",
    f"{WS_PREFIX}/lobby/{{match_id}}",
)


if __name__ == "__main__":
    import sys

    from logging_config import install_stream_tee

    install_stream_tee("auth_http")
    import uvicorn

    try:
        uvicorn.run(
            app,
            host=AUTH_SERVER_HOST,
            port=AUTH_SERVER_PORT,
            log_level="info",
            use_colors=sys.stderr.isatty(),
        )
    except KeyboardInterrupt:
        logger.info("Auth server stopped by user")
