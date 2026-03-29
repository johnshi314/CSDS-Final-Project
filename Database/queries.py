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
            connection.commit()
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
        print("Invalid JSON:", e)
        return

    if not isinstance(rows, list):
        print("JSON must be a list of player objects.")
        return

    required_fields = {"player_id", "hashedpw"}

    for i, row in enumerate(rows):
        if not required_fields.issubset(row.keys()):
            print(f"Row {i} is missing required fields. Required: {required_fields}")
            return

        # Add created_at timestamp
        row["created_at"] = datetime.now(timezone.utc)

    sql = db.text("""
        INSERT INTO players (player_id, created_at, hashedpw)
        VALUES (:player_id, :created_at, :hashedpw)
    """)

    try:
        with engine.begin() as connection:
            connection.execute(sql, rows)
            connection.commit()

        print(f"Inserted {len(rows)} players successfully!")

    except Exception as e:
        print("Error inserting players:", e)


def get_hashedpw(player_id):
    sql = db.text("SELECT hashedpw FROM players WHERE player_id = :player_id")
    try:
        with engine.connect() as connection:
            result = connection.execute(sql, {"player_id": player_id})
            row = result.fetchone()
            if row:
                return row[0]
            else:
                print(f"No player found with player_id = {player_id}")
                return None
    except Exception as e:
        print("Database error:", e)
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
            connection.commit()
        logger.info(f"Inserted {len(rows)} characters successfully!")
    except Exception as e:
        logger.error(f"Database error: {e}")


def insert_matches(json_string):
    try:
        rows = json.loads(json_string)
    except json.JSONDecodeError as e:
        print("Invalid JSON:", e)
        return

    for row in rows:
        if "start_time" in row and isinstance(row["start_time"], str):
            row["start_time"] = datetime.fromisoformat(row["start_time"].replace(" ", "T"))
        if "end_time" in row and isinstance(row["end_time"], str):
            row["end_time"] = datetime.fromisoformat(row["end_time"].replace(" ", "T"))

    sql = db.text("""
        INSERT INTO matches (match_id, start_time, end_time, duration, queue_time, winner_team_id)
        VALUES (:match_id, :start_time, :end_time, :duration, :queue_time, :winner_team_id)
    """)
    try:
        with engine.begin() as connection:
            connection.execute(sql, rows)
            connection.commit()
        print(f"Inserted {len(rows)} matches successfully!")
    except Exception as e:
        print("Database error:", e)


def insert_matchups(json_string):
    try:
        rows = json.loads(json_string)
    except json.JSONDecodeError as e:
        print("Invalid JSON:", e)
        return

    sql = db.text("""
        INSERT INTO matchup_stats (match_id, character_a_id, character_b_id, winner_character_id)
        VALUES (:match_id, :character_a_id, :character_b_id, :winner_character_id)
    """)
    try:
        with engine.begin() as connection:
            connection.execute(sql, rows)
            connection.commit()
        print(f"Inserted {len(rows)} matchups successfully!")
    except Exception as e:
        print("Database error:", e)


def insert_match_players(json_string):
    try:
        rows = json.loads(json_string)
    except json.JSONDecodeError as e:
        print("Invalid JSON:", e)
        return

    sql = db.text("""
        INSERT INTO player_match_stats (
            match_id, player_id, character_id, team_id,
            damage_dealt, damage_taken, turns_taken, won, disconnected
        )
        VALUES (
            :match_id, :player_id, :character_id, :team_id,
            :damage_dealt, :damage_taken, :turns_taken, :won, :disconnected
        )
    """)
    try:
        with engine.begin() as connection:
            connection.execute(sql, rows)
            connection.commit()
        print(f"Inserted {len(rows)} match_players successfully!")
    except Exception as e:
        print("Database error:", e)


def insert_ability_usage(json_string):
    try:
        rows = json.loads(json_string)
    except json.JSONDecodeError as e:
        print("Invalid JSON:", e)
        return

    sql = db.text("""
        INSERT INTO ability_usage_stats (character_id, player_id, damage_done, downtime, ability_name)
        VALUES (:character_id, :player_id, :damage_done, :downtime, :ability_name)
    """)
    try:
        with engine.begin() as connection:
            connection.execute(sql, rows)
            connection.commit()
        print(f"Inserted {len(rows)} ability_usage records successfully!")
    except Exception as e:
        print("Database error:", e)

# =============================
# New Match Functions
# =============================
def create_match(start_time=None, queue_time=0):
    """
    Create a new match row at the start of the game.
    Returns the auto-generated match_id.
    """
    try:
        if start_time is None:
            start_time = datetime.now(timezone.utc)

        try:
            with engine.begin() as connection:
                result = connection.execute(text("""
                    INSERT INTO matches (start_time, queue_time, lobby_status)
                    VALUES (:start_time, :queue_time, 'lobby')
                """), {
                    "start_time": start_time,
                    "queue_time": queue_time
                })
                match_id = result.lastrowid
        except Exception as e:
            logger.warning(
                "create_match: lobby_status insert failed (%s); retrying legacy INSERT",
                e,
            )
            with engine.begin() as connection:
                result = connection.execute(text("""
                    INSERT INTO matches (start_time, queue_time)
                    VALUES (:start_time, :queue_time)
                """), {
                    "start_time": start_time,
                    "queue_time": queue_time
                })
                match_id = result.lastrowid

        logger.info(f"Match created successfully (match_id: {match_id})")
        return match_id

    except Exception as e:
        logger.error(f"Error creating match: {e}")
        return None


def update_match(match_id, end_time, duration, winner_team_id):
    """
    Update match data when the match finishes.
    """
    try:
        if isinstance(end_time, str):
            end_time = datetime.fromisoformat(end_time.replace(" ", "T"))

        with engine.begin() as connection:
            connection.execute(text("""
                UPDATE matches
                SET end_time = :end_time,
                    duration = :duration,
                    winner_team_id = :winner_team_id
                WHERE match_id = :match_id
            """), {
                "match_id": match_id,
                "end_time": end_time,
                "duration": duration,
                "winner_team_id": winner_team_id
            })

            logger.info(f"Match {match_id} updated successfully")

    except Exception as e:
        logger.error(f"Error updating match: {e}")


# =============================
# Lobby (open match, pre-game)
# =============================
# Requires Database/migrations/001_lobby.sql applied to MySQL.

def get_open_lobby_match_for_player(player_id: int):
    """If player is already in a lobby-phase match, return that match_id."""
    try:
        with engine.connect() as connection:
            row = connection.execute(text("""
                SELECT lp.match_id
                FROM lobby_players lp
                INNER JOIN matches m ON m.match_id = lp.match_id
                WHERE lp.player_id = :player_id AND m.lobby_status = 'lobby'
                LIMIT 1
            """), {"player_id": player_id}).mappings().first()
            return int(row["match_id"]) if row else None
    except Exception as e:
        logger.error(f"get_open_lobby_match_for_player: {e}")
        return None


def find_joinable_lobby(max_players: int):
    """Return a lobby match_id with fewer than max_players members, or None."""
    try:
        with engine.connect() as connection:
            row = connection.execute(text("""
                SELECT m.match_id
                FROM matches m
                WHERE m.lobby_status = 'lobby'
                  AND (
                    SELECT COUNT(*) FROM lobby_players lp
                    WHERE lp.match_id = m.match_id
                  ) < :max_players
                ORDER BY m.match_id ASC
                LIMIT 1
            """), {"max_players": max_players}).mappings().first()
            return int(row["match_id"]) if row else None
    except Exception as e:
        logger.error(f"find_joinable_lobby: {e}")
        return None


def add_lobby_player(match_id: int, player_id: int) -> None:
    """Insert lobby row; raises on DB error (e.g. missing lobby_players table)."""
    with engine.begin() as connection:
        connection.execute(text("""
            INSERT INTO lobby_players (match_id, player_id, team, ready)
            VALUES (:match_id, :player_id, NULL, 0)
            ON DUPLICATE KEY UPDATE match_id = match_id
        """), {"match_id": match_id, "player_id": player_id})


def join_new_lobby(player_id: int, max_players: int = 8):
    """
    Put player into an open lobby (same match as other waiting players when possible),
    or create a new lobby match. Returns match_id or None on failure.
    """
    existing = get_open_lobby_match_for_player(player_id)
    if existing is not None:
        return existing
    mid = find_joinable_lobby(max_players)
    if mid is None:
        mid = create_match()
        if mid is None:
            return None
    add_lobby_player(mid, player_id)
    return mid


def set_lobby_team(match_id: int, player_id: int, team: str) -> bool | str:
    """Returns True on success, False on bad input/no-op, or an error string if the lobby is locked."""
    team = (team or "").lower().strip()
    if team not in ("red", "blue"):
        return False
    status = get_lobby_status(match_id)
    if status and status != "lobby":
        return f"Lobby is {status}"
    try:
        with engine.begin() as connection:
            r = connection.execute(text("""
                UPDATE lobby_players
                SET team = :team
                WHERE match_id = :match_id AND player_id = :player_id
            """), {"match_id": match_id, "player_id": player_id, "team": team})
            return r.rowcount > 0
    except Exception as e:
        logger.error(f"set_lobby_team: {e}")
        return False


def set_lobby_ready(match_id: int, player_id: int, ready: bool = True) -> bool | str:
    """Returns True on success, False on no-op, or an error string if the lobby is locked."""
    status = get_lobby_status(match_id)
    if status and status != "lobby":
        return f"Lobby is {status}"
    try:
        with engine.begin() as connection:
            r = connection.execute(text("""
                UPDATE lobby_players
                SET ready = :ready
                WHERE match_id = :match_id AND player_id = :player_id
            """), {"match_id": match_id, "player_id": player_id, "ready": 1 if ready else 0})
            return r.rowcount > 0
    except Exception as e:
        logger.error(f"set_lobby_ready: {e}")
        return False


def _lobby_rows(match_id: int):
    try:
        with engine.connect() as connection:
            return connection.execute(text("""
                SELECT player_id, team, ready
                FROM lobby_players
                WHERE match_id = :match_id
                ORDER BY player_id ASC
            """), {"match_id": match_id}).mappings().all()
    except Exception as e:
        logger.error("_lobby_rows: %s", e)
        return []


def get_lobby_snapshot(match_id: int) -> dict:
    """
    Snapshot for Unity JsonUtility: everyoneReady, lobbyStatus,
    redTeamPlayerIds, blueTeamPlayerIds (lists -> JSON arrays).
    """
    status = get_lobby_status(match_id) or "lobby"
    rows = _lobby_rows(match_id)
    red, blue = [], []
    everyone_ready = True
    if not rows:
        return {
            "everyoneReady": False,
            "lobbyStatus": status,
            "redTeamPlayerIds": [],
            "blueTeamPlayerIds": [],
        }
    for r in rows:
        pid = int(r["player_id"])
        team = r["team"]
        ready = bool(r["ready"])
        if team == "red":
            red.append(pid)
        elif team == "blue":
            blue.append(pid)
        if team not in ("red", "blue") or not ready:
            everyone_ready = False
    if len(red) < 1 or len(blue) < 1:
        everyone_ready = False
    return {
        "everyoneReady": everyone_ready,
        "lobbyStatus": status,
        "redTeamPlayerIds": red,
        "blueTeamPlayerIds": blue,
    }


def get_lobby_status(match_id: int) -> str | None:
    """Return the lobby_status column for a match, or None if not found."""
    try:
        with engine.connect() as connection:
            row = connection.execute(text(
                "SELECT lobby_status FROM matches WHERE match_id = :match_id"
            ), {"match_id": match_id}).mappings().first()
            return row["lobby_status"] if row else None
    except Exception as e:
        logger.error(f"get_lobby_status: {e}")
        return None


def is_player_in_lobby(match_id: int, player_id: int) -> bool:
    try:
        with engine.connect() as connection:
            row = connection.execute(text("""
                SELECT 1 FROM lobby_players
                WHERE match_id = :match_id AND player_id = :player_id
                LIMIT 1
            """), {"match_id": match_id, "player_id": player_id}).first()
            return row is not None
    except Exception as e:
        logger.error(f"is_player_in_lobby: {e}")
        return False


def mark_match_lobby_in_progress(match_id: int) -> bool:
    try:
        with engine.begin() as connection:
            connection.execute(text("""
                UPDATE matches SET lobby_status = 'in_progress'
                WHERE match_id = :match_id AND lobby_status = 'lobby'
            """), {"match_id": match_id})
        return True
    except Exception as e:
        logger.error(f"mark_match_lobby_in_progress: {e}")
        return False


# -----------------------------
# Test Data
# -----------------------------
players_json = json.dumps([
    {
        "player_id": 1,
        "hashedpw": "$2b$12$fakehash_player1"
    },
    {
        "player_id": 2,
        "hashedpw": "$2b$12$fakehash_player2"
    }
])

matches_json = json.dumps([
    {
        "match_id": 101,
        "start_time": "2026-02-19 14:00:00",
        "end_time": "2026-02-19 14:12:00",
        "duration": 720,
        "queue_time": 90,
        "winner_team_id": "ally"
    },
    {
        "match_id": 102,
        "start_time": "2026-02-19 15:30:00",
        "end_time": "2026-02-19 15:40:00",
        "duration": 700,
        "queue_time": 120,
        "winner_team_id": "enemy"
    },
    {
        "match_id": 103,
        "start_time": "2026-02-19 17:00:00",
        "end_time": "2026-02-19 17:08:00",
        "duration": 460,
        "queue_time": 100,
        "winner_team_id": "ally"
    },
    {
        "match_id": 104,
        "start_time": "2026-02-19 18:15:00",
        "end_time": "2026-02-19 18:20:00",
        "duration": 340,
        "queue_time": 80,
        "winner_team_id": "enemy"
    }
])

matchups_json = json.dumps([
    {
        "match_id": 101,
        "character_a_id": "Elf",
        "character_b_id": "Main Character",
        "winner_character_id": "Elf"
    },
    {
        "match_id": 101,
        "character_a_id": "Elf",
        "character_b_id": "CEO",
        "winner_character_id": "CEO"
    },
    {
        "match_id": 101,
        "character_a_id": "Elf",
        "character_b_id": "Delivery",
        "winner_character_id": "Elf"
    },
    {
        "match_id": 101,
        "character_a_id": "Elf",
        "character_b_id": "Main Character",
        "winner_character_id": "Elf"
    },
    {
        "match_id": 101,
        "character_a_id": "Elf",
        "character_b_id": "Main Character",
        "winner_character_id": "Main Character"
    }
])

player_match_json = json.dumps([
    {
        "match_player_id": 1,
        "match_id": 101,
        "player_id": 1,
        "character_id": "char_knight",
        "team_id": "ally",
        "damage_dealt": 850,
        "damage_taken": 400,
        "turns_taken": 8,
        "won": True,
        "disconnected": False
    },
    {
        "match_player_id": 2,
        "match_id": 102,
        "player_id": 1,
        "character_id": "char_knight",
        "team_id": "enemy",
        "damage_dealt": 900,
        "damage_taken": 350,
        "turns_taken": 7,
        "won": False,
        "disconnected": False
    },
        {
        "match_player_id": 3,
        "match_id": 103,
        "player_id": 1,
        "character_id": "char_knight",
        "team_id": "ally",
        "damage_dealt": 780,
        "damage_taken": 420,
        "turns_taken": 6,
        "won": True,
        "disconnected": False
    },
        {
        "match_player_id": 4,
        "match_id": 104,
        "player_id": 1,
        "character_id": "char_knight",
        "team_id": "enemy",
        "damage_dealt": 650,
        "damage_taken": 300,
        "turns_taken": 5,
        "won": False,
        "disconnected": False
    }
])

ability_usage_json = json.dumps([
    {
        "character_id": "Elf",
        "player_id": 2,
        "damage_done": 2,
        "downtime": 6.0,
        "ability_name": "ability 1"
    },
    {
        "character_id": "Elf",
        "player_id": 2,
        "damage_done": 4,
        "downtime": 4.0,
        "ability_name": "ability 2"
    },
    {
        "character_id": "Elf",
        "player_id": 2,
        "damage_done": 2,
        "downtime": 3.0,
        "ability_name": "ability 1"
    },
    {
        "character_id": "Elf",
        "player_id": 2,
        "damage_done": 2,
        "downtime": 2.0,
        "ability_name": "ability 1"
    },
    {
        "character_id": "Elf",
        "player_id": 2,
        "damage_done": 2,
        "downtime": 6.0,
        "ability_name": "ability 1"
    },
    {
        "character_id": "Elf",
        "player_id": 2,
        "damage_done": 3,
        "downtime": 4.0,
        "ability_name": "ability 3"
    },
    {
        "character_id": "Elf",
        "player_id": 2,
        "damage_done": 3,
        "downtime": 10.0,
        "ability_name": "ability 4"
    },
    {
        "character_id": "Elf",
        "player_id": 2,
        "damage_done": 6,
        "downtime": 7.0,
        "ability_name": "ability 4"
    },
    {
        "character_id": "Elf",
        "player_id": 2,
        "damage_done": 2,
        "downtime": 1.0,
        "ability_name": "ability 1"
    },
    {
        "character_id": "Elf",
        "player_id": 2,
        "damage_done": 2,
        "downtime": 1.0,
        "ability_name": "ability 1"
    },
    {
        "character_id": "Elf",
        "player_id": 2,
        "damage_done": 4,
        "downtime": 4.0,
        "ability_name": "ability 2"
    },
    {
        "character_id": "Elf",
        "player_id": 2,
        "damage_done": 4,
        "downtime": 4.0,
        "ability_name": "ability 2"
    }
])

if __name__ == "__main__":
    # =============================
    # Run Test Inserts
    # =============================
    with engine.begin() as connection:
        connection.execute(db.text("DELETE FROM ability_usage_stats"))
        connection.execute(db.text("DELETE FROM player_match_stats"))
        connection.execute(db.text("DELETE FROM matchup_stats"))
        connection.execute(db.text("DELETE FROM matches"))
        connection.execute(db.text("DELETE FROM players"))

    insert_players(players_json)
    insert_matches(matches_json)
    insert_matchups(matchups_json)
    insert_match_players(player_match_json)
    insert_ability_usage(ability_usage_json)

    print("All test inserts completed!")
