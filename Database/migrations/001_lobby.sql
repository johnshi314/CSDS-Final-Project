-- Lobby support for NetFlower (run once against your MySQL DB).
-- Adds match lobby phase tracking and per-player lobby rows.

ALTER TABLE matches
  ADD COLUMN lobby_status VARCHAR(32) NOT NULL DEFAULT 'lobby'
  COMMENT 'lobby | in_progress | completed';

CREATE TABLE IF NOT EXISTS lobby_players (
  match_id INT NOT NULL,
  player_id INT NOT NULL,
  team VARCHAR(16) NULL COMMENT 'red | blue',
  ready TINYINT(1) NOT NULL DEFAULT 0,
  joined_at DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (match_id, player_id),
  KEY idx_lobby_match (match_id),
  CONSTRAINT fk_lobby_match FOREIGN KEY (match_id) REFERENCES matches (match_id)
    ON DELETE CASCADE
);
