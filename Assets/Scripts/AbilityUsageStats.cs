using UnityEngine;
using System;

namespace NetFlower {

    [System.Serializable]
    public class AbilityUsageStats {

        // Identifiers 
        public int characterId;      
        public int matchPlayerId;    

        // Metrics 
        public int damageDone;
        public float downtime;       

        public AbilityUsageStats(
            int characterId,
            int matchPlayerId
        ) {
            this.characterId = characterId;
            this.matchPlayerId = matchPlayerId;

            damageDone = 0;
            downtime = 0f;
        }

        public string ToJson() {
            return JsonUtility.ToJson(this);
        }
    }
}

