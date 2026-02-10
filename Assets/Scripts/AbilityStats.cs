namespace GameManager {

    [System.Serializable]
    public class AbilityUsageStats {

        // Identifiers 
        public int abilityId;
        public int characterId;
        public int usageId;          
        public int matchPlayerId;    

        // Metrics 
        public int damageDone;
        public float downtime;       

        public AbilityUsageStats(
            int abilityId,
            int characterId,
            int usageId,
            int matchPlayerId
        ) {
            this.abilityId = abilityId;
            this.characterId = characterId;
            this.usageId = usageId;
            this.matchPlayerId = matchPlayerId;

            damageDone = 0;
            downtime = 0f;
        }
    }
}

