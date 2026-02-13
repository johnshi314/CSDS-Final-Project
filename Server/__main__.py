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
import time

if __name__ == "__main__":
    processes = []
    
    def signal_handler(signum, frame):
        """Handle SIGINT"""
        print("\n\nShutting down servers...")
        for p in processes:
            if p.poll() is None:
                p.terminate()
        sys.exit(0)
    
    # Register signal handler
    signal.signal(signal.SIGINT, signal_handler)
    
    try:
        # Create two processes: one for HTTP server, one for WebSocket server
        print("Starting HTTP Auth Server...")
        http_process = subprocess.Popen([sys.executable, "-m", "Server.auth_http"])
        processes.append(http_process)
        time.sleep(0.5)  # Give HTTP server a moment to start
        
        print("Starting WebSocket Server...")
        websocket_process = subprocess.Popen([sys.executable, "-m", "Server.multiplayer_echo"])
        processes.append(websocket_process)
        
        print("\nServers running. Press Ctrl+C to shutdown.\n")
        
        # Wait on both — if either exits, we'll fall through
        for p in processes:
            p.wait()
    except KeyboardInterrupt:
        print("\n\nShutting down servers...")
    finally:
        # Shut down any still-running processes
        for p in processes:
            if p.poll() is None:
                p.terminate()
        for p in processes:
            p.wait()
        print("All servers stopped.")
