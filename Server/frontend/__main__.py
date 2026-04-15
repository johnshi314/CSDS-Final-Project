"""
Entry point: ``python -m Server.frontend``

From the repository root with ``.env`` configured (see ``.env.example``).
Requires ``python -m Server.api`` (or compatible backend) for ``/api/*`` proxy.
"""
from Server.frontend.server import run

if __name__ == "__main__":
    run()
