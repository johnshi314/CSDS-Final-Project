DROP TABLE IF EXISTS `ability_usage_stats`;
CREATE TABLE `ability_usage_stats` (
  `ability_usage_id` int NOT NULL AUTO_INCREMENT,
  `character_id` varchar(255) NOT NULL,
  `player_id` int NOT NULL,
  `damage_done` int DEFAULT '0',
  `downtime` float DEFAULT '0',
  `ability_name` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`ability_usage_id`),
  KEY `fk_ability_usage_player` (`player_id`),
  CONSTRAINT `fk_ability_usage_player` FOREIGN KEY (`player_id`) REFERENCES `players` (`player_id`) ON DELETE CASCADE ON UPDATE CASCADE
);
DROP TABLE IF EXISTS `matches`;
CREATE TABLE `matches` (
  `match_id` int NOT NULL AUTO_INCREMENT,
  `start_time` datetime DEFAULT NULL,
  `end_time` datetime DEFAULT NULL,
  `duration` float DEFAULT NULL,
  `queue_time` float DEFAULT NULL,
  `winner_team_id` varchar(50) DEFAULT NULL,
  PRIMARY KEY (`match_id`)
);
DROP TABLE IF EXISTS `matchup_stats`;
CREATE TABLE `matchup_stats` (
  `matchup_id` int NOT NULL AUTO_INCREMENT,
  `match_id` int NOT NULL,
  `character_a_id` varchar(50) NOT NULL,
  `character_b_id` varchar(50) NOT NULL,
  `winner_character_id` varchar(50) DEFAULT NULL,
  PRIMARY KEY (`matchup_id`),
  KEY `match_id` (`match_id`),
  CONSTRAINT `matchup_stats_ibfk_1` FOREIGN KEY (`match_id`) REFERENCES `matches` (`match_id`) ON DELETE CASCADE ON UPDATE CASCADE
);
DROP TABLE IF EXISTS `player_match_stats`;
CREATE TABLE `player_match_stats` (
  `match_player_id` int NOT NULL AUTO_INCREMENT,
  `match_id` int NOT NULL,
  `player_id` int NOT NULL,
  `character_id` varchar(50) NOT NULL,
  `team_id` varchar(50) DEFAULT NULL,
  `damage_dealt` int DEFAULT '0',
  `damage_taken` int DEFAULT '0',
  `turns_taken` int DEFAULT '0',
  `won` tinyint(1) DEFAULT '0',
  `disconnected` tinyint(1) DEFAULT '0',
  PRIMARY KEY (`match_player_id`),
  KEY `match_id` (`match_id`),
  KEY `player_id` (`player_id`),
  CONSTRAINT `player_match_stats_ibfk_1` FOREIGN KEY (`match_id`) REFERENCES `matches` (`match_id`) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT `player_match_stats_ibfk_2` FOREIGN KEY (`player_id`) REFERENCES `players` (`player_id`) ON DELETE CASCADE ON UPDATE CASCADE
);
DROP TABLE IF EXISTS `players`;
CREATE TABLE `players` (
  `player_id` int NOT NULL AUTO_INCREMENT,
  `username` varchar(255) NOT NULL,
  `hashedpw` varchar(255) NOT NULL,
  `created_at` datetime DEFAULT CURRENT_TIMESTAMP,
  `elo` int DEFAULT '1000',
  PRIMARY KEY (`player_id`)
);

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

