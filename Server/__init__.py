##############################################################
# File: Server/__init__.py
# Author: Mikey Maldonado
# Description: Initialize the Server package.
# Date Created: 2026-01-31
##############################################################
import os
import sys
# Add project root to sys.path for imports
parent_dir = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if parent_dir not in sys.path:
    sys.path.append(parent_dir)
