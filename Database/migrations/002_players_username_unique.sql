-- Run once on existing databases after adding `username` to `players`.
-- Fails if duplicate usernames exist; resolve duplicates first.
ALTER TABLE `players`
  ADD UNIQUE KEY `uk_players_username` (`username`);
