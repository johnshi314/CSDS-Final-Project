"""
HTTP Auth Server for Unity Game
Provides REST endpoints for registration, login, and token verification.
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


if __name__ == "__main__":
    import uvicorn
    try:
        uvicorn.run(app, host=AUTH_SERVER_HOST, port=AUTH_SERVER_PORT, log_level="info")
    except KeyboardInterrupt:
        logger.info("Auth server stopped by user")
