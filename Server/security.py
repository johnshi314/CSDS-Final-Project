from datetime import datetime, timedelta, timezone
import os

import jwt
from fastapi import HTTPException, Request, Security, WebSocket
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer

JWT_SECRET = os.getenv("JWT_SECRET", "change_this_secret_key")
JWT_ALGORITHM = os.getenv("JWT_ALGORITHM", "HS256")
JWT_EXPIRATION_HOURS = int(os.getenv("JWT_EXPIRATION_HOURS", 24))

_bearer_scheme = HTTPBearer(auto_error=False)


def generate_jwt_token(player_id: int) -> str:
    payload = {
        "player_id": player_id,
        "exp": datetime.now(timezone.utc) + timedelta(hours=JWT_EXPIRATION_HOURS),
        "iat": datetime.now(timezone.utc),
    }
    return jwt.encode(payload, JWT_SECRET, algorithm=JWT_ALGORITHM)


def verify_jwt_token(token: str):
    try:
        return jwt.decode(token, JWT_SECRET, algorithms=[JWT_ALGORITHM])
    except jwt.ExpiredSignatureError:
        return None
    except jwt.InvalidTokenError:
        return None


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


def get_player_id_from_websocket(websocket: WebSocket) -> int | None:
    """Extract player_id from a WebSocket's ?authToken= query param or auth_token cookie."""
    token = websocket.query_params.get("authToken")
    if not token:
        token = websocket.cookies.get("auth_token")
    if not token:
        return None
    payload = verify_jwt_token(token)
    return payload["player_id"] if payload else None
