ALTER TABLE `lobby_players`
    ADD COLUMN `character_id` INT DEFAULT NULL
    AFTER `ready`;
