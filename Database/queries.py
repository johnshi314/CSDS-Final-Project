import sqlalchemy as db
import json
from datetime import datetime, timezone

# Create engine
engine = db.create_engine('mysql+pymysql://root:mysqlPASSWORD@localhost:3306/netflower')

# Inspect tables
inspector = db.inspect(engine)
print("Tables:", inspector.get_table_names())

# -----------------------------
# Insert Functions
# -----------------------------

def insert_players(json_string):
    try:
        rows = json.loads(json_string)
    except json.JSONDecodeError as e:
        print("Invalid JSON:", e)
        return

    if not isinstance(rows, list):
        print("JSON must be a list of player objects.")
        return

    required_fields = {"player_id", "username", "hashedpw"}

    for i, row in enumerate(rows):
        if not required_fields.issubset(row.keys()):
            print(f"Row {i} is missing required fields. Required: {required_fields}")
            return

        # Add created_at timestamp
        row["created_at"] = datetime.now(timezone.utc)

    sql = db.text("""
        INSERT INTO players (player_id, username, created_at, hashedpw)
        VALUES (:player_id, :username, :created_at, :hashedpw)
    """)

    try:
        with engine.begin() as connection:
            connection.execute(sql, rows)

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
        INSERT INTO matchup_stats (matchup_id, match_id, character_a_id, character_b_id, winner_character_id)
        VALUES (:matchup_id, :match_id, :character_a_id, :character_b_id, :winner_character_id)
    """)
    try:
        with engine.begin() as connection:
            connection.execute(sql, rows)
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
        INSERT INTO ability_usage_stats (match_player_id, ability_id, damage_done, downtime)
        VALUES (:match_player_id, :ability_id, :damage_done, :downtime)
    """)
    try:
        with engine.begin() as connection:
            connection.execute(sql, rows)
        print(f"Inserted {len(rows)} ability_usage records successfully!")
    except Exception as e:
        print("Database error:", e)

# -----------------------------
# Test Data
# -----------------------------
players_json = json.dumps([
    {
        "player_id": 1,
        "username": "player1",
        "hashedpw": "$2b$12$fakehash_player1"
    },
    {
        "player_id": 2,
        "username": "player2",
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
    }
])

matchups_json = json.dumps([
    {
        "matchup_id": 1,
        "match_id": 101,
        "character_a_id": "char_knight",
        "character_b_id": "char_mage",
        "winner_character_id": "char_mage",
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
        "match_id": 101,
        "player_id": 2,
        "character_id": "char_mage",
        "team_id": "enemy",
        "damage_dealt": 600,
        "damage_taken": 700,
        "turns_taken": 7,
        "won": False,
        "disconnected": False
    }
])

ability_usage_json = json.dumps([
    {
        "match_player_id": 1,
        "ability_id": 101,
        "damage_done": 250,
        "downtime": 4.5
    },
    {
        "match_player_id": 2,
        "ability_id": 102,
        "damage_done": 300,
        "downtime": 6.0
    }
])


# -----------------------------
# Run Test Inserts
# -----------------------------
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

