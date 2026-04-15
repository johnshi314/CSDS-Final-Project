"""
Load a single `.env` file into the process environment (via python-dotenv).

Call `load_repo_dotenv(base_dir=...)` once from each entrypoint. `base_dir` should be
the directory that contains `.env` (repository root in development, or `WORKDIR` in
containers when `repo_dotenv.py` and `.env` are copied or mounted there).

Second and later calls are no-ops (idempotent).
"""
from __future__ import annotations

from pathlib import Path

_done = False


def load_repo_dotenv(*, base_dir: Path | None = None) -> None:
    global _done
    if _done:
        return
    try:
        from dotenv import load_dotenv as _load_dotenv
    except ImportError:
        _done = True
        return

    root = Path(base_dir).resolve() if base_dir is not None else Path(__file__).resolve().parent
    env_path = root / ".env"
    if env_path.is_file():
        _load_dotenv(env_path)
    _done = True
