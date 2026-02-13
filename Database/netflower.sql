-- Create the database if it doesn't exist
CREATE DATABASE IF NOT EXISTS netflower;
USE netflower;

-- -----------------------------
-- Players
-- -----------------------------
CREATE TABLE IF NOT EXISTS players (
    player_id INT PRIMARY KEY AUTO_INCREMENT,
    created_at DATETIME NOT NULL,
    hashedpw VARCHAR(255) NOT NULL
);

-- -----------------------------
-- Characters
-- -----------------------------
CREATE TABLE IF NOT EXISTS characters (
    character_id INT PRIMARY KEY,
    name VARCHAR(100) NOT NULL
);

-- -----------------------------
-- Matches
-- -----------------------------
CREATE TABLE IF NOT EXISTS matches (
    match_id INT PRIMARY KEY,
    start_time DATETIME,
    end_time DATETIME,
    duration INT,
    queue_time INT,
    winner_team_id INT
);

-- -----------------------------
-- Matchups
-- -----------------------------
CREATE TABLE IF NOT EXISTS matchups (
    matchup_id INT PRIMARY KEY,
    match_id INT NOT NULL,
    character_a_id INT NOT NULL,
    character_b_id INT NOT NULL,
    winner_character INT,
    duration INT,
    FOREIGN KEY (match_id) REFERENCES matches(match_id),
    FOREIGN KEY (character_a_id) REFERENCES characters(character_id),
    FOREIGN KEY (character_b_id) REFERENCES characters(character_id),
    FOREIGN KEY (winner_character) REFERENCES characters(character_id)
);

-- -----------------------------
-- Abilities Picked
-- -----------------------------
CREATE TABLE IF NOT EXISTS abilities_picked (
    ability_id INT PRIMARY KEY,
    character_id INT NOT NULL,
    FOREIGN KEY (character_id) REFERENCES characters(character_id)
);

-- -----------------------------
-- Match Players
-- -----------------------------
CREATE TABLE IF NOT EXISTS match_players (
    match_player_id INT PRIMARY KEY,
    match_id INT NOT NULL,
    player_id INT NOT NULL,
    character_id INT NOT NULL,
    team_id INT NOT NULL,
    damage_dealt INT DEFAULT 0,
    damage_taken INT DEFAULT 0,
    turns_taken INT DEFAULT 0,
    won BOOLEAN DEFAULT FALSE,
    disconnected BOOLEAN DEFAULT FALSE,
    FOREIGN KEY (match_id) REFERENCES matches(match_id),
    FOREIGN KEY (player_id) REFERENCES players(player_id),
    FOREIGN KEY (character_id) REFERENCES characters(character_id)
);

-- -----------------------------
-- Ability Usage
-- -----------------------------
CREATE TABLE IF NOT EXISTS ability_usage (
    usage_id INT PRIMARY KEY,
    match_player_id INT NOT NULL,
    ability_id INT NOT NULL,
    damage_done INT DEFAULT 0,
    downtime DECIMAL(10, 2) DEFAULT 0.00,
    FOREIGN KEY (match_player_id) REFERENCES match_players(match_player_id),
    FOREIGN KEY (ability_id) REFERENCES abilities_picked(ability_id)
);
