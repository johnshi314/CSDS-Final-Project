#!/usr/bin/env python3
"""
Setup script for Unity project.
- Installs Git hooks (pre-commit and post-merge)
- Detects Unity installations and configures UnityYAMLMerge tool
"""

from pathlib import Path
from datetime import datetime
import platform
import subprocess
from typing import List, Optional
import shutil
import sys
import configparser
import logging


# Setup logging
def setup_logger(project_dir: Path) -> logging.Logger:
    """Setup logger with both file and console handlers."""
    # Create logs directory
    logs_dir = project_dir / "logs"
    logs_dir.mkdir(exist_ok=True)
    
    # Create logger
    logger = logging.getLogger("setup")
    logger.setLevel(logging.DEBUG)
    
    # Prevent duplicate handlers
    if logger.handlers:
        logger.handlers.clear()
    
    # File handler - detailed logging
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    file_handler = logging.FileHandler(logs_dir / f"setup_{timestamp}.log")
    file_handler.setLevel(logging.DEBUG)
    file_formatter = logging.Formatter(
        "%(asctime)s - %(levelname)s - %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S"
    )
    file_handler.setFormatter(file_formatter)
    
    # Console handler - user-friendly output
    console_handler = logging.StreamHandler(sys.stdout)
    console_handler.setLevel(logging.INFO)
    console_formatter = logging.Formatter("%(message)s")
    console_handler.setFormatter(console_formatter)
    
    logger.addHandler(file_handler)
    logger.addHandler(console_handler)
    
    return logger


def is_git_repository(path: Path) -> bool:
    """Check if the given path is a git repository."""
    git_dir = path / ".git"
    return git_dir.exists() and git_dir.is_dir()


def backup_hook(hook_path: Path, logger: logging.Logger) -> None:
    """Backup an existing hook file with a timestamp."""
    if hook_path.exists():
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        backup_path = hook_path.parent / f"{hook_path.name}_{timestamp}"
        shutil.copy2(hook_path, backup_path)
        logger.debug(f"Backed up existing {hook_path.name} to {backup_path.name}")


def install_hook(source_path: Path, hooks_dir: Path, logger: logging.Logger) -> None:
    """Install a hook file to the .git/hooks directory."""
    if not source_path.exists():
        logger.warning(f"Source file {source_path.name} not found, skipping.")
        return
    
    # Ensure hooks directory exists
    hooks_dir.mkdir(parents=True, exist_ok=True)
    
    dest_path = hooks_dir / source_path.name
    
    # Backup existing hook if it exists
    backup_hook(dest_path, logger)
    
    # Copy the hook
    shutil.copy2(source_path, dest_path)
    
    # Make it executable (Unix/Linux/macOS only)
    try:
        dest_path.chmod(0o755)
    except (OSError, NotImplementedError):
        # Windows doesn't support chmod, but Git on Windows handles hook permissions
        pass
    
    logger.info(f"Installed {source_path.name} to {dest_path}")


def detect_unity_version(project_path: Path, logger: logging.Logger) -> Optional[str]:
    """Detect Unity version from ProjectVersion.txt file."""
    version_file = project_path / "ProjectSettings" / "ProjectVersion.txt"
    if not version_file.exists():
        return None
    
    try:
        with version_file.open("r") as vf:
            for line in vf:
                if line.startswith("m_EditorVersion:"):
                    return line.split(":", 1)[1].strip()
    except Exception as e:
        logger.warning(f"Could not read Unity version: {e}")
    
    return None


def find_unity_installations() -> List[Path]:
    """Find all Unity installations on the system."""
    installations = []
    system = platform.system()
    
    if system == "Darwin":  # macOS
        base_path = Path("/Applications/Unity/Hub/Editor")
        if base_path.exists():
            for version_dir in base_path.iterdir():
                unity_app = version_dir / "Unity.app"
                if unity_app.exists():
                    installations.append(version_dir)
    
    elif system == "Windows":
        base_paths = [
            Path("C:/Program Files/Unity/Hub/Editor"),
            Path("C:/Program Files (x86)/Unity/Hub/Editor"),
        ]
        for base_path in base_paths:
            if base_path.exists():
                for version_dir in base_path.iterdir():
                    unity_exe = version_dir / "Editor" / "Unity.exe"
                    if unity_exe.exists():
                        installations.append(version_dir)
    
    elif system == "Linux":
        base_paths = [
            Path.home() / "Unity/Hub/Editor",
            Path("/opt/Unity/Hub/Editor"),
        ]
        for base_path in base_paths:
            if base_path.exists():
                for version_dir in base_path.iterdir():
                    unity_bin = version_dir / "Editor" / "Unity"
                    if unity_bin.exists():
                        installations.append(version_dir)
    
    return sorted(installations, key=lambda p: p.name, reverse=True)


def get_unity_yaml_merge_path(unity_install_path: Path) -> Optional[Path]:
    """Get the UnityYAMLMerge tool path for a Unity installation."""
    system = platform.system()
    
    if system == "Darwin":  # macOS
        yaml_merge = unity_install_path / "Unity.app" / "Contents" / "Tools" / "UnityYAMLMerge"
    elif system == "Windows":
        yaml_merge = unity_install_path / "Editor" / "Data" / "Tools" / "UnityYAMLMerge.exe"
    elif system == "Linux":
        yaml_merge = unity_install_path / "Editor" / "Data" / "Tools" / "UnityYAMLMerge"
    else:
        return None
    
    return yaml_merge if yaml_merge.exists() else None


def select_unity_version(installations: List[Path], project_version: Optional[str], logger: logging.Logger) -> Optional[Path]:
    """Automatically select matching Unity version, or prompt user if not found."""
    if not installations:
        logger.info("\nNo Unity installations found. Skipping UnityYAMLMerge configuration.")
        return None
    
    # Find the last installation that matches the project version (if available)
    matching_installation = None
    
    if project_version:
        matching_indices = [
            i for i, install in enumerate(installations)
            if project_version in install.name
        ]
        if matching_indices:
            matching_index = matching_indices[-1]  # Get the last match
            matching_installation = installations[matching_index]
            logger.info(f"Found matching Unity installation: {matching_installation.name}")
            return matching_installation
    
    # No match found, prompt user to select from list
    logger.info(f"Project requires version {project_version}, but no matching installation found.")
    logger.info("\nAvailable Unity installations:")
    
    for i, install in enumerate(installations, 1):
        version = install.name
        marker = " (default)" if i == 1 else ""
        logger.info(f"  {i}. {version}{marker}")
    
    logger.info(f"  {len(installations) + 1}. Skip (do not configure UnityYAMLMerge)")
    
    while True:
        try:
            prompt = f"\nSelect Unity version (1-{len(installations)}, or {len(installations) + 1} to skip) [default: 1]: "
            choice = input(prompt).strip()
            
            # If empty input, default to first installation
            if choice == "":
                choice_num = 1
            else:
                choice_num = int(choice)
            
            if choice_num == len(installations) + 1:  # Skip option
                return None
            
            if 1 <= choice_num <= len(installations):
                return installations[choice_num - 1]
            
            logger.info(f"Please enter a number between 1 and {len(installations) + 1}")
        except ValueError:
            logger.info("Invalid input. Please enter a valid number.")
        except KeyboardInterrupt:
            logger.info("\nSkipping Unity configuration.")
            return None


def check_git_available() -> bool:
    """Check if git command is available."""
    try:
        result = subprocess.run(
            ["git", "--version"],
            capture_output=True,
            timeout=5
        )
        return result.returncode == 0
    except (FileNotFoundError, subprocess.TimeoutExpired):
        return False


def configure_git_with_command(project_path: Path, yaml_merge_path: Path, logger: logging.Logger) -> bool:
    """Configure Git using git command (preferred method)."""
    try:
        commands = [
            ["git", "config", "mergetool.unityyamlmerge.trustExitCode", "false"],
            ["git", "config", "mergetool.unityyamlmerge.cmd", 
             f"'{yaml_merge_path}' merge -p \"$BASE\" \"$REMOTE\" \"$LOCAL\" \"$MERGED\""],
        ]
        
        for cmd in commands:
            result = subprocess.run(cmd, cwd=project_path, capture_output=True, text=True)
            if result.returncode != 0:
                logger.warning(f"Failed to run: {' '.join(cmd)}")
                logger.debug(f"Error: {result.stderr}")
                return False
            else:
                logger.info(' '.join(cmd))
        
        return True
    except Exception as e:
        logger.error(f"Git command error: {e}")
        return False


def configure_git_with_configparser(project_path: Path, yaml_merge_path: Path, logger: logging.Logger) -> bool:
    """Configure Git by directly editing .git/config (fallback method)."""
    try:
        git_config_path = project_path / ".git" / "config"
        
        if not git_config_path.exists():
            logger.error(".git/config not found")
            return False
        
        # Read existing config
        config = configparser.ConfigParser()
        # Make sure keys are case-sensitive
        config.optionxform = str
        # Git config allows duplicate keys, but we'll use RawConfigParser for simplicity
        config.read(git_config_path)
        
        # Add or update mergetool section
        section = 'mergetool "unityyamlmerge"'
        if not config.has_section(section):
            config.add_section(section)
        
        config.set(section, "trustExitCode", "false")
        config.set(section, "cmd", f"'{yaml_merge_path}' merge -p \"$BASE\" \"$REMOTE\" \"$LOCAL\" \"$MERGED\"")
        
        # Write back to file
        with git_config_path.open('w') as configfile:
            config.write(configfile, space_around_delimiters=True)
        
        return True
    except Exception as e:
        logger.error(f"ConfigParser error: {e}")
        return False


def configure_unity_yaml_merge(project_path: Path, unity_path: Path, logger: logging.Logger) -> bool:
    """Configure UnityYAMLMerge as a merge tool in Git."""
    yaml_merge_path = get_unity_yaml_merge_path(unity_path)

    if not yaml_merge_path:
        logger.error(f"UnityYAMLMerge not found in {unity_path}")
        return False
    
    logger.info(f"Configuring UnityYAMLMerge: {yaml_merge_path}")
    
    git_dir = project_path / ".git"
    if not git_dir.exists():
        logger.error(".git directory not found")
        return False
    
    # Try git command first, fall back to configparser
    git_available = check_git_available()
    # git_available = False  # Force fallback for testing purposes


    if git_available:
        success = configure_git_with_command(project_path, yaml_merge_path, logger)
    else:
        logger.info("Git command not found, using direct config edit...")
        success = configure_git_with_configparser(project_path, yaml_merge_path, logger)
    
    if success:
        logger.info("UnityYAMLMerge configured successfully")
    else:
        logger.error("UnityYAMLMerge configuration failed")
    
    return success

def main():
    # Get the directory where this script is located
    script_dir = Path(__file__).resolve().parent
    
    # Setup logger
    logger = setup_logger(script_dir)
    
    logger.info("=" * 60)
    logger.info("Unity Project Setup")
    logger.info("=" * 60)
    
    # Check if this is a git repository
    if not is_git_repository(script_dir):
        logger.error("\nThis directory does not appear to be a git repository.")
        logger.error(f"Expected to find .git directory in: {script_dir}")
        sys.exit(1)
    
    logger.info(f"Git repository detected: {script_dir}")
    
    # Step 1: Install Git hooks
    logger.info("-" * 60)
    logger.info("Step 1: Installing Git Hooks")
    logger.info("-" * 60)
    
    hooks_dir = script_dir / ".git" / "hooks"
    pre_commit_source = script_dir / "pre-commit"
    post_merge_source = script_dir / "post-merge"
    
    hooks_dir.mkdir(parents=True, exist_ok=True)
    
    install_hook(pre_commit_source, hooks_dir, logger)
    install_hook(post_merge_source, hooks_dir, logger)
    
    logger.info("Git hooks installed")
    
    # Step 2: Configure Unity
    logger.info("-" * 60)
    logger.info("Step 2: Unity Configuration")
    logger.info("-" * 60)
    
    project_version = detect_unity_version(script_dir, logger)
    if project_version:
        logger.info(f"Project Unity version: {project_version}")
    else:
        logger.warning("Could not detect project Unity version")
    
    installations = find_unity_installations()
    
    if installations:
        selected_unity = select_unity_version(installations, project_version, logger)
        
        if selected_unity:
            configure_unity_yaml_merge(script_dir, selected_unity, logger)
        else:
            logger.info("\nSkipping Unity configuration.")
    else:
        logger.warning("\nNo Unity installations found. Skipping Unity configuration.")
        logger.info("UnityYAMLMerge will not be configured.")
    
    # Done
    logger.info("Setup Complete!")


if __name__ == "__main__":
    main()
