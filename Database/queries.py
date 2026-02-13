import sys
import sqlalchemy as db
import json
import os
from pathlib import Path
from datetime import datetime, timezone
from sqlalchemy import text

# Add project root to path for importing project modules and variables
_project_root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
if _project_root not in sys.path:
    sys.path.append(_project_root)

# Log output to file with timestamps
from logging_config import get_logger
logger = get_logger(__name__)

# Load environment variables (e.g. for DB connection)
from dotenv import load_dotenv
load_dotenv(
    dotenv_path=Path(_project_root) / ".env"
)


# Create engine
engine = db.create_engine('mysql+pymysql://{}:{}@{}:{}/{}'.format(
    os.getenv('DB_USER'),
    os.getenv('DB_PASSWORD'),
    os.getenv('DB_HOST'),
    os.getenv('DB_PORT'),
    os.getenv('DB_NAME')))

# Inspect tables
inspector = db.inspect(engine)
print("Tables:", inspector.get_table_names())

# =============================
# User Authentication Functions
# =============================

def create_player(hashedpw):
    """Create a new player account with hashed password. Returns generated player_id."""
    try:
        with engine.begin() as connection:
            result = connection.execute(text(
                "INSERT INTO players (created_at, hashedpw) VALUES (:created_at, :hashedpw)"
            ), {"created_at": datetime.now(timezone.utc), "hashedpw": hashedpw})
            player_id = result.lastrowid
            logger.info(f"Player created successfully (ID: {player_id})")
            return player_id
    except Exception as e:
        logger.error(f"Error creating player: {e}")
        return None


def get_player_by_id(player_id):
    """Retrieve player by ID"""
    try:
        with engine.connect() as connection:
            result = connection.execute(text(
                "SELECT player_id, created_at, hashedpw FROM players WHERE player_id = :player_id"
            ), {"player_id": player_id})
            row = result.mappings().first()
            return dict(row) if row else None
    except Exception as e:
        logger.error(f"Error retrieving player: {e}")
        return None


# =============================
# Game Data Insert Functions
# =============================

def insert_players(json_string):
    try:
        rows = json.loads(json_string)
    except json.JSONDecodeError as e:
        logger.error(f"Invalid JSON: {e}")
        return

    for row in rows:
        if "player_id" not in row or "hashedpw" not in row:
            logger.error("Each object must contain player_id and hashedpw")
            return
        row["created_at"] = datetime.now(timezone.utc)

    sql = text("INSERT INTO players (player_id, created_at, hashedpw) VALUES (:player_id, :created_at, :hashedpw)")

    try:
        with engine.begin() as connection:
            connection.execute(sql, rows)
        logger.info(f"Inserted {len(rows)} players successfully!")
    except Exception as e:
        logger.error(f"Error inserting players: {e}")


def get_hashedpw(player_id):
    sql = text("SELECT hashedpw FROM players WHERE player_id = :player_id")
    try:
        with engine.connect() as connection:
            result = connection.execute(sql, {"player_id": player_id})
            row = result.fetchone()
            if row:
                return row[0]
            else:
                logger.warning(f"No player found with player_id = {player_id}")
                return None
    except Exception as e:
        logger.error(f"Database error: {e}")
        return None


def insert_characters(json_string):
    try:
        rows = json.loads(json_string)
    except json.JSONDecodeError as e:
        logger.error(f"Invalid JSON: {e}")
        return

    sql = text("INSERT INTO characters (character_id, name) VALUES (:character_id, :name)")
    try:
        with engine.begin() as connection:
            connection.execute(sql, rows)
        logger.info(f"Inserted {len(rows)} characters successfully!")
    except Exception as e:
        logger.error(f"Database error: {e}")


def insert_matches(json_string):
    try:
        rows = json.loads(json_string)
    except json.JSONDecodeError as e:
        logger.error(f"Invalid JSON: {e}")
        return

    for row in rows:
        if "start_time" in row and isinstance(row["start_time"], str):
            row["start_time"] = datetime.fromisoformat(row["start_time"].replace(" ", "T"))
        if "end_time" in row and isinstance(row["end_time"], str):
            row["end_time"] = datetime.fromisoformat(row["end_time"].replace(" ", "T"))

    sql = text("""
        INSERT INTO matches (match_id, start_time, end_time, duration, queue_time, winner_team_id)
        VALUES (:match_id, :start_time, :end_time, :duration, :queue_time, :winner_team_id)
    """)
    try:
        with engine.begin() as connection:
            connection.execute(sql, rows)
        logger.info(f"Inserted {len(rows)} matches successfully!")
    except Exception as e:
        logger.error(f"Database error: {e}")


def insert_matchups(json_string):
    try:
        rows = json.loads(json_string)
    except json.JSONDecodeError as e:
        logger.error(f"Invalid JSON: {e}")
        return

    sql = text("""
        INSERT INTO matchups (matchup_id, match_id, character_a_id, character_b_id, winner_character, duration)
        VALUES (:matchup_id, :match_id, :character_a_id, :character_b_id, :winner_character, :duration)
    """)
    try:
        with engine.begin() as connection:
            connection.execute(sql, rows)
        logger.info(f"Inserted {len(rows)} matchups successfully!")
    except Exception as e:
        logger.error(f"Database error: {e}")


def insert_match_players(json_string):
    try:
        rows = json.loads(json_string)
    except json.JSONDecodeError as e:
        logger.error(f"Invalid JSON: {e}")
        return

    sql = text("""
        INSERT INTO match_players (
            match_player_id, match_id, player_id, character_id, team_id,
            damage_dealt, damage_taken, turns_taken, won, disconnected
        )
        VALUES (
            :match_player_id, :match_id, :player_id, :character_id, :team_id,
            :damage_dealt, :damage_taken, :turns_taken, :won, :disconnected
        )
    """)
    try:
        with engine.begin() as connection:
            connection.execute(sql, rows)
        logger.info(f"Inserted {len(rows)} match_players successfully!")
    except Exception as e:
        logger.error(f"Database error: {e}")


def insert_ability_usage(json_string):
    try:
        rows = json.loads(json_string)
    except json.JSONDecodeError as e:
        logger.error(f"Invalid JSON: {e}")
        return

    sql = text("""
        INSERT INTO ability_usage (usage_id, match_player_id, ability_id, damage_done, downtime)
        VALUES (:usage_id, :match_player_id, :ability_id, :damage_done, :downtime)
    """)
    try:
        with engine.begin() as connection:
            connection.execute(sql, rows)
        logger.info(f"Inserted {len(rows)} ability_usage records successfully!")
    except Exception as e:
        logger.error(f"Database error: {e}")


def insert_abilities_picked(json_string):
    try:
        rows = json.loads(json_string)
    except json.JSONDecodeError as e:
        logger.error(f"Invalid JSON: {e}")
        return

    sql = text("INSERT INTO abilities_picked (ability_id, character_id) VALUES (:ability_id, :character_id)")
    try:
        with engine.begin() as connection:
            connection.execute(sql, rows)
        logger.info(f"Inserted {len(rows)} abilities_picked records successfully!")
    except Exception as e:
        logger.error(f"Database error: {e}")


# -----------------------------
# Test Data
# -----------------------------
players_json = json.dumps([
    {"player_id": 1, "hashedpw": "$2b$12$fakehash1"},
    {"player_id": 2, "hashedpw": "$2b$12$fakehash2"}
])

characters_json = json.dumps([
    {"character_id": 1, "name": "Warrior"},
    {"character_id": 2, "name": "Mage"}
])

matches_json = json.dumps([
    {
        "match_id": 101,
        "start_time": "2026-02-11 14:00:00",
        "end_time": "2026-02-11 14:10:00",
        "duration": 600,
        "queue_time": 120,
        "winner_team_id": 1
    }
])

matchups_json = json.dumps([
    {
        "matchup_id": 1,
        "match_id": 101,
        "character_a_id": 1,
        "character_b_id": 2,
        "winner_character": 1,
        "duration": 300
    }
])

match_players_json = json.dumps([
    {
        "match_player_id": 1,
        "match_id": 101,
        "player_id": 1,
        "character_id": 1,
        "team_id": 1,
        "damage_dealt": 500,
        "damage_taken": 200,
        "turns_taken": 5,
        "won": True,
        "disconnected": False
    }
])

ability_usage_json = json.dumps([
    {
        "usage_id": 1,
        "match_player_id": 1,
        "ability_id": 101,
        "damage_done": 150,
        "downtime": 5.0
    }
])

abilities_picked_json = json.dumps([
    {"ability_id": 101, "character_id": 1},
    {"ability_id": 102, "character_id": 2}
])

if __name__ == "__main__":
    # =============================
    # Run Test Inserts
    # =============================
    insert_players(players_json)
    insert_characters(characters_json)
    insert_matches(matches_json)
    insert_matchups(matchups_json)
    insert_match_players(match_players_json)
    insert_abilities_picked(abilities_picked_json)
    insert_ability_usage(ability_usage_json)

    logger.info("All test inserts completed!")
