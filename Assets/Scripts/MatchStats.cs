using System;

namespace GameManager {

    [System.Serializable]
    public class MatchData {

        // Primary Key
        public int matchId;

        // Timing
        public DateTime startTime;
        public DateTime endTime;
        public float duration;      
        public float queueTime;     

        // Outcome
        public int winnerTeamId;

        public MatchData(
            int matchId,
            float queueTime = 0f
        ) {
            this.matchId = matchId;
            this.queueTime = queueTime;

            startTime = DateTime.UtcNow;
            endTime = default;
            duration = 0f;
            winnerTeamId = -1; 
        }

        public void EndMatch(int winnerTeamId) {
            this.winnerTeamId = winnerTeamId;
            endTime = DateTime.UtcNow;
            duration = (float)(endTime - startTime).TotalSeconds;
        }
    }
}

