using System;

namespace NetFlower {

    [System.Serializable]
    public class MatchupStats {

        // Primary Key
        public int matchupId;

        // Foreign Keys
        public int matchId;
        public int characterAId;
        public int characterBId;

        // Outcome
        public int winnerCharacterId;


        public MatchupStats(
            int matchupId,
            int matchId,
            int characterAId,
            int characterBId
        ) {
            this.matchupId = matchupId;
            this.matchId = matchId;
            this.characterAId = characterAId;
            this.characterBId = characterBId;

            winnerCharacterId = -1;
        }
    }
}

