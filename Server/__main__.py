##############################################################
# File: Server/__main__.py
# Author: Mikey Maldonado
# Description: Entry point to run the WebSocket echo server.
# Date Created: 2026-01-31
##############################################################
"""
Run the WebSocket echo server: python -m Server
"""
import sys
import signal
import subprocess

if __name__ == "__main__":
    processes = []
    try:
        # Create two processes: one for HTTP server, one for WebSocket server
        http_process = subprocess.Popen([sys.executable, "-m", "Server.auth_http"])
        processes.append(http_process)
        websocket_process = subprocess.Popen([sys.executable, "-m", "Server.multiplayer_echo"])
        processes.append(websocket_process)

        # Wait on both — if either exits, we'll fall through
        for p in processes:
            p.wait()
    except KeyboardInterrupt:
        pass
    finally:
        # Shut down any still-running processes
        for p in processes:
            if p.poll() is None:
                p.terminate()
        for p in processes:
            p.wait()
