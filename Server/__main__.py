##############################################################
# File: Server/__main__.py
# Description: Run the unified HTTP + lobby WebSocket server (FastAPI).
# Optional: run turn-based demo WebSocket on port 8765 in a second terminal.
##############################################################
"""
Run the game backend: HTTP (auth, stats, lobby REST) + lobby WebSocket on the same port.

    python -m Server

Uses one process so lobby state and WebSocket fan-out share memory. For the separate
turn-based echo demo (epoch/turn protocol), run in another terminal:

    python -m Server.multiplayer_echo
"""
import sys
import signal

if __name__ == "__main__":
    def signal_handler(signum, frame):
        sys.exit(0)

    signal.signal(signal.SIGINT, signal_handler)

    import uvicorn
    from logging_config import get_logger

    logger = get_logger(__name__)

    import os
    host = os.getenv("AUTH_SERVER_HOST", "0.0.0.0")
    port = int(os.getenv("AUTH_SERVER_PORT", 8000))

    logger.info("Starting unified server on http://%s:%s (lobby WS: /ws/lobby/{match_id})", host, port)
    uvicorn.run("Server.auth_http:app", host=host, port=port, log_level="info")
