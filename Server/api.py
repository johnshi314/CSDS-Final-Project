"""
HTTP Auth Server for Unity Game
Provides REST endpoints for registration, login, recordkeeping, and token verification.
"""
import os
from contextlib import asynccontextmanager

import bcrypt
import json

from fastapi import (
    APIRouter,
    Depends,
    FastAPI,
    HTTPException,
    Request,
    Response,
)
from pydantic import BaseModel, Field
from sqlalchemy.exc import IntegrityError

from Database import queries
from logging_config import get_logger
from Server.battle_ws import router as battle_ws_router
from Server.lobby_ws import router as lobby_ws_router
from Server.security import generate_jwt_token, get_current_player, verify_jwt_token

logger = get_logger(__name__)

# Auth Server Configuration
AUTH_SERVER_HOST = os.getenv("AUTH_SERVER_HOST", "0.0.0.0")
AUTH_SERVER_PORT = int(os.getenv("AUTH_SERVER_PORT", 8000))

# Reverse-proxy path prefixes (e.g. nginx: /api/* and /ws/* → this app)
def _norm_prefix(p: str) -> str:
    p = (p or "").strip()
    if not p:
        return ""
    return "/" + p.strip("/")


API_PREFIX = _norm_prefix(os.getenv("API_PREFIX", ""))
WS_PREFIX = _norm_prefix(os.getenv("WS_PREFIX", "/ws")) or "/ws"

# JWT Configuration
JWT_EXPIRATION_HOURS = int(os.getenv("JWT_EXPIRATION_HOURS", 24))


@asynccontextmanager
async def lifespan(app: FastAPI):
    # No table initialization needed - using existing schema
    logger.info("Auth server started")
    yield
    logger.info("Auth server shutdown")


app = FastAPI(title="Unity Auth Server", lifespan=lifespan)
api_router = APIRouter()


@app.get("/health")
def health():
    """Liveness probe; does not touch the database. Use for watchdogs and load balancers."""
    return {"status": "ok"}


# Lobby fan-out and websocket routes live in Server.lobby_runtime and Server.lobby_ws.


class RegisterRequest(BaseModel):
    password: str = Field(min_length=8)
    username: str = Field(min_length=1, max_length=255)


class LoginRequest(BaseModel):
    username: str
    password: str


def _normalize_username(name: str) -> str:
    return (name or "").strip()


class TokenVerifyRequest(BaseModel):
    authToken: str


class PlayerMatchStatsRequest(BaseModel):
    matchId: int
    playerId: int | None = None
    characterId: str
    teamId: str
    damageDealt: int
    damageTaken: int
    turnsTaken: int
    won: bool
    disconnected: bool


class MatchupStatsRequest(BaseModel):
    matchId: int
    characterAId: str
    characterBId: str
    winnerCharacterId: str


class AbilityUsageStatsRequest(BaseModel):
    characterId: str
    playerId: int | None = None
    damageDone: int
    downtime: int
    abilityName: str


class UpdateMatchRequest(BaseModel):
    matchId: int
    endTime: str
    duration: float
    winnerTeamId: str


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
        raise HTTPException(status_code=401, detail="Invalid username or password")

    password_match = bcrypt.checkpw(
        password.encode("utf-8"),
        player["hashedpw"].encode("utf-8"),
    )

    if not password_match:
        raise HTTPException(status_code=401, detail="Invalid username or password")

    player_id = player["player_id"]
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
        "playerId": verified['player_id']
    }

@api_router.post("/submit-playermatchstats")
def submit_playermatchstats(
    stat: PlayerMatchStatsRequest,
    authenticated_player_id: int = Depends(get_current_player),
):
    """
    Accepts a PlayerMatchStats JSON object from Unity
    and inserts it into the database
    """
    try:
        claimed_player_id = stat.playerId
        if claimed_player_id is not None and claimed_player_id != authenticated_player_id:
            raise HTTPException(status_code=403, detail="playerId does not match authenticated user")

        converted_row = {
            # remove match_player_id for AUTO_INCREMENT
            "match_player_id": None,
            "match_id": stat.matchId,
            "player_id": authenticated_player_id,
            "character_id": stat.characterId,
            "team_id": stat.teamId,
            "damage_dealt": stat.damageDealt,
            "damage_taken": stat.damageTaken,
            "turns_taken": stat.turnsTaken,
            "won": stat.won,
            "disconnected": stat.disconnected,
        }
        json_string = json.dumps([converted_row])
        queries.insert_match_players(json_string)

        return {
            "status": "success",
            "message": "Player Match stat inserted successfully"
        }

    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
   
@api_router.post("/submit-matchupstats", dependencies=[Depends(get_current_player)])
async def submit_matchupstats(matchup: MatchupStatsRequest):
    """
    Accepts a MatchStats JSON object from Unity and inserts into database.
    """
    try:

        row = {
            "matchup_id": None,
            "match_id": matchup.matchId,
            "character_a_id": matchup.characterAId,
            "character_b_id": matchup.characterBId,
            "winner_character_id": matchup.winnerCharacterId,
        }

        queries.insert_matchups(json.dumps([row]))
        return {"status": "success", "message": "MatchupStats inserted successfully"}

    except Exception as e:
        logger.exception("Submit abilitystats failed")
        raise HTTPException(status_code=500, detail=str(e))

@api_router.post("/submit-abilityusagestats")
async def submit_abilityusagestats(
    ability: AbilityUsageStatsRequest,
    authenticated_player_id: int = Depends(get_current_player),
):
    """
    Accepts an AbilityUsageStats JSON object from Unity and inserts into database.
    """
    try:
        claimed_player_id = ability.playerId
        if claimed_player_id is not None and claimed_player_id != authenticated_player_id:
            raise HTTPException(status_code=403, detail="playerId does not match authenticated user")

        row = {
            "ability_usage_id": None,
            "character_id": ability.characterId,
            "player_id": authenticated_player_id,
            "damage_done": ability.damageDone,
            "downtime": ability.downtime,
            "ability_name" : ability.abilityName,
        }

        queries.insert_ability_usage(json.dumps([row]))
        return {"status": "success", "message": "AbilityStats inserted successfully"}

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

@api_router.post("/update-match", dependencies=[Depends(get_current_player)])
def update_match(match: UpdateMatchRequest):
    """
    Updates match stats when the game ends.
    """
    try:
        queries.update_match(
            match_id=match.matchId,
            end_time=match.endTime,
            duration=match.duration,
            winner_team_id=match.winnerTeamId,
        )

        return {
            "status": "success",
            "message": "Match updated successfully"
        }

    except Exception as e:
        logger.exception("Update match failed")
        raise HTTPException(status_code=500, detail=str(e))


app.include_router(api_router, prefix=API_PREFIX)
# If routes are at root, also mount under /api so nginx can proxy https://host/api/* unchanged.
if not API_PREFIX:
    app.include_router(api_router, prefix="/api")
app.include_router(lobby_ws_router, prefix=WS_PREFIX)
app.include_router(battle_ws_router, prefix=WS_PREFIX)
logger.info(
    "HTTP API mounted at %r%s; WebSocket endpoints at %r and %r",
    API_PREFIX or "/",
    " and /api" if not API_PREFIX else "",
    f"{WS_PREFIX}/lobby/{{match_id}}",
    f"{WS_PREFIX}/battle/{{match_id}}",
)


if __name__ == "__main__":
    import sys

    from logging_config import install_stream_tee

    install_stream_tee("api")
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
