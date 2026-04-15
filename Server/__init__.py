##############################################################
# File: Server/__init__.py
# Author: Mikey Maldonado
# Description: Initialize the Server package.
# Date Created: 2026-01-31
##############################################################
import sys
from pathlib import Path

# Add project root to sys.path for imports
_project_root = Path(__file__).parent.parent.resolve()
if str(_project_root) not in sys.path:
    sys.path.insert(0, str(_project_root))

from repo_dotenv import load_repo_dotenv

load_repo_dotenv(base_dir=_project_root)
