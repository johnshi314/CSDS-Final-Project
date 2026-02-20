using UnityEngine;
using System;

namespace NetFlower {

    [System.Serializable]
    public class PlayerMatchStats {

        // Identifiers  
        public int matchId;         
        public int playerId;        
        public string characterId;     
        public string teamId;          

        // metrics
        public int damageDealt;
        public int damageTaken;
        public int turnsTaken;
        public bool won;
        public bool disconnected;

        public PlayerMatchStats(
            int matchId,
            int playerId,
            string characterId,
            string teamId
        ) {
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

        public string ToJson() {
            return JsonUtility.ToJson(this);
        }
    }
}
