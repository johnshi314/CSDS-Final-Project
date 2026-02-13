##############################################################
# File: Server/__init__.py
# Author: Mikey Maldonado
# Description: Initialize the Server package.
# Date Created: 2026-01-31
##############################################################
import sys
from pathlib import Path
from dotenv import load_dotenv
# Add project root to sys.path for imports
_project_root = Path(__file__).parent.parent.resolve()
if _project_root not in sys.path:
    sys.path.append(_project_root)
load_dotenv(dotenv_path=Path(_project_root) / ".env")
