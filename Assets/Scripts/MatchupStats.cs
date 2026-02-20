using UnityEngine;
using System;

namespace NetFlower {

    [System.Serializable]
    public class MatchupStats {

        // Foreign Keys
        public int matchId;
        public string characterAId;
        public string characterBId;

        // Outcome
        public string winnerCharacterId;


        public MatchupStats(
            int matchId,
            string characterAId,
            string characterBId
        ) {
            this.matchId = matchId;
            this.characterAId = characterAId;
            this.characterBId = characterBId;

            winnerCharacterId = "-1";
        }

        public string ToJson() {
            return JsonUtility.ToJson(this);
        }
    }
}

