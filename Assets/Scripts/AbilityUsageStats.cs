using UnityEngine;
using System;

namespace NetFlower {

    [System.Serializable]
    public class AbilityUsageStats {

        // Identifiers
        public string characterId;
        public int playerId;
        public string abilityName;

        // Metrics
        public int damageDone;
        public float downtime;

        public AbilityUsageStats(
            string characterId,
            int playerId,
            string abilityName
        ) {
            this.characterId = characterId;
            this.playerId = playerId;
            this.abilityName = abilityName;

            damageDone = 0;
            downtime = 0f;
        }

        public string ToJson() {
            return JsonUtility.ToJson(this);
        }
    }
}

