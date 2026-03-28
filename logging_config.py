"""
Centralized Python logging: structured logs under logs/ plus optional stdout/stderr tee
for containers (still mirrors to the real streams so podman logs works).
"""
import logging
import os
import sys
import inspect
from pathlib import Path
from datetime import datetime

_stream_tee_files: list[object] = []


def get_log_dir() -> Path:
    """Project log directory: env LOG_DIR, else repo-root logs/ (next to this file)."""
    raw = os.getenv("LOG_DIR", "").strip()
    if raw:
        p = Path(raw)
        return p if p.is_absolute() else (Path(__file__).resolve().parent / p).resolve()
    return (Path(__file__).resolve().parent / "logs").resolve()


def _stream_tee_enabled() -> bool:
    return os.getenv("PYTHON_STREAM_TEE", "1").strip().lower() not in ("0", "false", "no", "off")


class TeeIO:
    """Write to the real tty/pipe and to a log file (line-friendly, flushed often)."""

    __slots__ = ("_primary", "_file")

    def __init__(self, primary, fileobj):
        self._primary = primary
        self._file = fileobj

    def write(self, data):
        if not data:
            return 0
        self._primary.write(data)
        self._primary.flush()
        self._file.write(data)
        self._file.flush()
        return len(data)

    def flush(self):
        self._primary.flush()
        self._file.flush()

    def isatty(self):
        return self._primary.isatty()

    def fileno(self):
        return self._primary.fileno()


def install_stream_tee(process_name: str) -> None:
    """
    Append stdout/stderr to logs/{process_name}.stdout.log and .stderr.log
    while still writing to the original streams (podman/docker logs, systemd).
    No-op if already installed or PYTHON_STREAM_TEE=0.
    """
    if not _stream_tee_enabled():
        return
    if isinstance(sys.stdout, TeeIO):
        return

    log_dir = get_log_dir()
    log_dir.mkdir(parents=True, exist_ok=True)

    out_path = log_dir / f"{process_name}.stdout.log"
    err_path = log_dir / f"{process_name}.stderr.log"
    out_f = open(out_path, "a", encoding="utf-8", buffering=1)
    err_f = open(err_path, "a", encoding="utf-8", buffering=1)
    _stream_tee_files.extend([out_f, err_f])

    sys.stdout = TeeIO(sys.__stdout__, out_f)
    sys.stderr = TeeIO(sys.__stderr__, err_f)


def setup_logger(name: str, level=logging.INFO):
    """
    Set up a logger that writes to both console and a module-specific file under logs/.
    """
    log_dir = get_log_dir()
    log_dir.mkdir(parents=True, exist_ok=True)

    if name == "__main__":
        frame = inspect.currentframe()
        if frame and frame.f_back and frame.f_back.f_back:
            caller_file = frame.f_back.f_back.f_code.co_filename
            module_name = Path(caller_file).stem
        else:
            module_name = "__main__"
    else:
        module_name = name.split(".")[-1]

    timestamp = datetime.now().strftime("%Y%m%d")
    log_file = log_dir / f"{module_name}_{timestamp}.log"

    logger = logging.getLogger(name)
    logger.setLevel(level)

    if logger.handlers:
        return logger

    detailed_formatter = logging.Formatter(
        "%(asctime)s - %(name)s - %(levelname)s - %(funcName)s:%(lineno)d - %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S",
    )

    console_formatter = logging.Formatter(
        "%(asctime)s - %(name)s - %(levelname)s - %(message)s",
        datefmt="%H:%M:%S",
    )

    file_handler = logging.FileHandler(log_file, mode="a", encoding="utf-8")
    file_handler.setLevel(level)
    file_handler.setFormatter(detailed_formatter)

    console_handler = logging.StreamHandler()
    console_handler.setLevel(level)
    console_handler.setFormatter(console_formatter)

    logger.addHandler(file_handler)
    logger.addHandler(console_handler)

    return logger


def get_logger(name: str, level=logging.INFO):
    return setup_logger(name, level)
