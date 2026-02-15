using UnityEngine;
using System.Collections.Generic;

// Placeholder
namespace NetFlower {
    public class Match : MonoBehaviour {

        // Match stats
        public MatchData matchData { get; private set; }

        // Player stats
        private List<PlayerMatchStats> playerStatsList = new();
        private Dictionary<int, PlayerMatchStats> playerStatsDict = new();

        // Matchup stats
        private List<MatchupStats> matchupStatsList = new();
        private Dictionary<(int, int), MatchupStats> matchupLookup = new();

        // Match state
        private bool matchActive = false;
        private int nextMatchPlayerId = 1;
        private int nextMatchupId = 1;

        public void Start() {
            StartMatch();

            // Test player
            RegisterPlayer(
                playerId: 1,
                characterId: 101,
                teamId: 0);

            // Create parent objects for allies and enemies
            GameObject allies = new GameObject("Allies");
            GameObject enemies = new GameObject("Enemies");

            // Create test agents
            GameObject newAlly = Agent.NewAgent(
                player: null,
                agent_name: "Test Ally 1",
                hp: 30,
                range: 3,
                abilities: null,
                tunneling: Agent.Tunneling.Ally,
                gameObjectName: null,
                parent: allies,
                position: new Vector3(2, 0, 0)
            );
            GameObject newAgent = Agent.NewAgent(
                player: null,
                agent_name: "Test Enemy 1",
                hp: 15,
                range: 2,
                abilities: null,
                tunneling: Agent.Tunneling.Nothing,
                gameObjectName: null,
                parent: enemies,
                position: new Vector3(0, 0, 0)
            );

            // Add sphere mesh to both agents
            MeshFilter enemyMesh = newAgent.AddComponent<MeshFilter>();
            enemyMesh.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            MeshFilter allyMesh = newAlly.AddComponent<MeshFilter>();
            allyMesh.mesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");

            // Make blue material
            Material blueMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            blueMaterial.color = Color.blue;
            // Make red material
            Material redMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            redMaterial.color = Color.red;

            // Apply blue material to ally agent
            MeshRenderer allyRenderer = newAlly.AddComponent<MeshRenderer>();
            allyRenderer.materials = new Material[] { blueMaterial };
            // Apply red material to enemy agent
            MeshRenderer enemyRenderer = newAgent.AddComponent<MeshRenderer>();
            enemyRenderer.materials = new Material[] { redMaterial };
        }
        // Record match data at start and end
        public void StartMatch() {
            if (matchActive) return;
            matchData = new MatchData(
                matchId: 1,
                queueTime: 0f
             );
            matchActive = true;
        }

        public void EndMatch(int winnerTeamId) {
            if (!matchActive) return;
            matchData.EndMatch(winnerTeamId);

            foreach (var stats in playerStatsList)
                stats.won = (stats.teamId == winnerTeamId);

            ResolveMatchups();
            matchActive = false;
        }

        public PlayerMatchStats RegisterPlayer(
            int playerId,
            int characterId,
            int teamId
        ) {
            var stats = new PlayerMatchStats(
                matchPlayerId: nextMatchPlayerId++,
                matchId: matchData.matchId,
                playerId: playerId,
                characterId: characterId,
                teamId: teamId);

            playerStatsList.Add(stats);
            playerStatsDict[stats.matchPlayerId] = stats;
            return stats;
        }


        public void RegisterMatchup(int characterAId, int characterBId) {
            var key = GetMatchupKey(characterAId, characterBId);

            if (matchupLookup.ContainsKey(key))
                return;

            var matchup = new MatchupStats(
                matchupId: nextMatchupId++,
                matchId: matchData.matchId,
                characterAId: key.Item1,
                characterBId: key.Item2
            );

            matchupStatsList.Add(matchup);
            matchupLookup.Add(key, matchup);
        }

        public void ResolveMatchups() {
            foreach (var matchup in matchupStatsList) {

                var aStats = playerStatsList.Find(
                    p => p.characterId == matchup.characterAId
                );

                var bStats = playerStatsList.Find(
                    p => p.characterId == matchup.characterBId
                );

                if (aStats == null || bStats == null)
                    continue;

                if (aStats.won && !bStats.won)
                    matchup.winnerCharacterId = aStats.characterId;
                else if (bStats.won && !aStats.won)
                    matchup.winnerCharacterId = bStats.characterId;
                else
                    matchup.winnerCharacterId = -1; // tie 
            }
        }


        // Methods to record player stats during game
        public void RecordDamageDealt(int matchPlayerId, int amount) {
            if (!playerStatsDict.TryGetValue(matchPlayerId, out var stats)) return;
            stats.damageDealt += amount;
        }

        public void RecordDamageTaken(int matchPlayerId, int amount) {
            if (!playerStatsDict.TryGetValue(matchPlayerId, out var stats)) return;
            stats.damageTaken += amount;
        }

        public void RecordTurnTaken(int matchPlayerId) {
            if (!playerStatsDict.TryGetValue(matchPlayerId, out var stats)) return;
            stats.turnsTaken++;
        }

        public void RecordDisconnect(int matchPlayerId) {
            if (!playerStatsDict.TryGetValue(matchPlayerId, out var stats)) return;
            stats.disconnected = true;
        }

        private (int, int) GetMatchupKey(int characterAId, int characterBId) {
            if (characterAId < characterBId)
                return (characterAId, characterBId);
            else
                return (characterBId, characterAId);
        }
    }
}
