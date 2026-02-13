"""
Centralized Python logging configuration
Creates separate log files for each module in the Logs/ directory.
"""
import logging
import os
import inspect
from pathlib import Path
from datetime import datetime


def setup_logger(name: str, level=logging.INFO):
    """
    Set up a logger that writes to both console and a module-specific file.
    
    Args:
        name: Logger name (typically __name__ from the calling module)
        level: Logging level (default: INFO)
    
    Returns:
        Configured logger instance
    """
    # Create logs directory if it doesn't exist (using lowercase 'logs' for Python logs)
    log_dir = Path(__file__).parent / "Logs"
    log_dir.mkdir(exist_ok=True)
    
    # If name is __main__, try to get the actual module name from the caller's file
    if name == "__main__":
        frame = inspect.currentframe()
        if frame and frame.f_back and frame.f_back.f_back:
            caller_file = frame.f_back.f_back.f_code.co_filename
            module_name = Path(caller_file).stem  # Get filename without extension
        else:
            module_name = "__main__"
    else:
        # Extract module name from full path (e.g., "Server.auth_http" -> "auth_http")
        module_name = name.split('.')[-1]
    
    # Create timestamp for log session
    timestamp = datetime.now().strftime("%Y%m%d")
    log_file = log_dir / f"{module_name}_{timestamp}.log"
    
    # Create logger - use the original name for logger identity
    logger = logging.getLogger(name)
    logger.setLevel(level)
    
    # Prevent duplicate handlers if logger already exists
    if logger.handlers:
        return logger
    
    # Create formatters
    detailed_formatter = logging.Formatter(
        '%(asctime)s - %(name)s - %(levelname)s - %(funcName)s:%(lineno)d - %(message)s',
        datefmt='%Y-%m-%d %H:%M:%S'
    )
    
    console_formatter = logging.Formatter(
        '%(asctime)s - %(name)s - %(levelname)s - %(message)s',
        datefmt='%H:%M:%S'
    )
    
    # File handler - detailed logs
    file_handler = logging.FileHandler(log_file, mode='a', encoding='utf-8')
    file_handler.setLevel(level)
    file_handler.setFormatter(detailed_formatter)
    
    # Console handler - simpler format
    console_handler = logging.StreamHandler()
    console_handler.setLevel(level)
    console_handler.setFormatter(console_formatter)
    
    # Add handlers to logger
    logger.addHandler(file_handler)
    logger.addHandler(console_handler)
    
    return logger


def get_logger(name: str, level=logging.INFO):
    """
    Get or create a logger for the specified module.
    
    Args:
        name: Logger name (use __name__ from calling module)
        level: Logging level (default: INFO)
    
    Returns:
        Configured logger instance
    """
    return setup_logger(name, level)
