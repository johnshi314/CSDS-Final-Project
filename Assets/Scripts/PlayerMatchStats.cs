namespace GameManager {

    [System.Serializable]
    public class PlayerMatchStats {

        // Identifiers
        public int matchPlayerId;   
        public int matchId;         
        public int playerId;        
        public int characterId;     
        public int teamId;          

        // metrics
        public int damageDealt;
        public int damageTaken;
        public int turnsTaken;
        public bool won;
        public bool disconnected;

        public PlayerMatchStats(
            int matchPlayerId,
            int matchId,
            int playerId,
            int characterId,
            int teamId
        ) {
            this.matchPlayerId = matchPlayerId;
            this.matchId = matchId;
            this.playerId = playerId;
            this.characterId = characterId;
            this.teamId = teamId;

            damageDealt = 0;
            damageTaken = 0;
            turnsTaken = 0;
            won = false;
            disconnected = false;
        }
    }
}
