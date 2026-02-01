##############################################################
# File: Server/__main__.py
# Author: Mikey Maldonado
# Description: Entry point to run the WebSocket echo server.
# Date Created: 2026-01-31
##############################################################
"""
Run the WebSocket echo server: python -m Server
"""
import asyncio
from .multiplayer_echo import main

if __name__ == "__main__":
    asyncio.run(main())
