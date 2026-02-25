using System;
using UnityEngine;

namespace NetFlower {

    [System.Serializable]
    public class MatchStats {

        // PK
        public int matchId;

        // Timing
        public string startTime;
        public string endTime;
        public float duration;      
        public float queueTime;     

        // Outcome
        public string winnerTeamId;

        public MatchStats() { }

        public MatchStats(
            int matchId,
            float queueTime = 0f
        ) {
            this.matchId = matchId;
            this.queueTime = queueTime;

            startTime = default;
            endTime = default;
            duration = 0f;
            winnerTeamId = "-1"; 
        }

        public string ToJson() {
            return JsonUtility.ToJson(this);
        }
    }
}

