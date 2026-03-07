"""
HTTP Auth Server for Unity Game
Provides REST endpoints for registration, login, recordkeeping, and token verification.
"""
from datetime import datetime, timedelta, timezone
import os
from contextlib import asynccontextmanager

import bcrypt
import jwt
from fastapi import FastAPI, HTTPException, Request, Response
from pydantic import BaseModel, Field

from Database import queries
from logging_config import get_logger

from fastapi import HTTPException
import json

logger = get_logger(__name__)

# Auth Server Configuration
AUTH_SERVER_HOST = os.getenv("AUTH_SERVER_HOST", "0.0.0.0")
AUTH_SERVER_PORT = int(os.getenv("AUTH_SERVER_PORT", 8000))

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


class RegisterRequest(BaseModel):
    password: str = Field(min_length=8)


class LoginRequest(BaseModel):
    player_id: int
    password: str


class TokenVerifyRequest(BaseModel):
    token: str


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


@app.post("/register")
def register(payload: RegisterRequest, response: Response):
    password = payload.password

    password_hash = bcrypt.hashpw(password.encode('utf-8'), bcrypt.gensalt())

    player_id = queries.create_player(password_hash.decode('utf-8'))

    if not isinstance(player_id, int) or player_id is None:
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
        "player_id": player_id,
        "token": token
    }


@app.post("/login")
def login(payload: LoginRequest, request: Request, response: Response):
    player_id = payload.player_id
    password = payload.password

    if player_id <= 0 or not password:
        raise HTTPException(status_code=400, detail="Player ID and password required")

    player = queries.get_player_by_id(player_id)
    if not player:
        raise HTTPException(status_code=401, detail="Invalid player ID or password")

    password_match = bcrypt.checkpw(
        password.encode('utf-8'),
        player['hashedpw'].encode('utf-8')
    )

    if not password_match:
        raise HTTPException(status_code=401, detail="Invalid player ID or password")

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
        "player_id": player_id,
        "token": token
    }


@app.post("/verify")
def verify_token(payload: TokenVerifyRequest):
    token = payload.token
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
        "player_id": verified['player_id']
    }

@app.post("/submit-playermatchstats")
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
   
@app.post("/submit-matchupstats")
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

@app.post("/submit-abilityusagestats")
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

@app.post("/create-match")
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

@app.post("/update-match")
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



if __name__ == "__main__":
    import uvicorn
    try:
        uvicorn.run(app, host=AUTH_SERVER_HOST, port=AUTH_SERVER_PORT, log_level="info")
    except KeyboardInterrupt:
        logger.info("Auth server stopped by user")
