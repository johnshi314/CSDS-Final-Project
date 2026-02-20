using UnityEngine;
using System.Collections.Generic;
using System;

// Placeholder
namespace NetFlower {
    public class Match : MonoBehaviour {

        // Match stats
        public MatchStats matchStats { get; private set; }

        // Match state
        private bool matchActive = false;

        // Variables for stats
        private PlayerMatchStats allyStats;
        private PlayerMatchStats enemyStats;
        private MatchupStats matchupStats;
        private int dbMatchId;

        public void Start() {
            // change this to fetch pk from db
            dbMatchId = 1;
            StartMatch(dbMatchId);

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

            // Store stats on newAlly
            Agent allyAgent = newAlly.GetComponent<Agent>();
            allyStats = allyAgent.RegisterPlayer(dbMatchId);

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

            // Store stats on newAgent
            Agent enemyAgent = newAgent.GetComponent<Agent>();
            enemyStats = enemyAgent.RegisterPlayer(dbMatchId);

            // Track matchup stats
            matchupStats = RegisterMatchup(allyAgent.GetAgentName(), enemyAgent.GetAgentName());

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
        public void StartMatch(int dbMatchId) {
            if (matchActive) return;
            matchStats = new MatchStats(
                matchId: dbMatchId,
                queueTime: 0f
             );
            matchStats.startTime = DateTime.UtcNow;
            matchActive = true;
        }

        public void EndMatch(string winnerTeamId) {
            if (!matchActive) return;

            matchStats.endTime = DateTime.UtcNow;
            matchStats.duration = (float)(matchStats.endTime - matchStats.startTime).TotalSeconds;

            matchStats.winnerTeamId = winnerTeamId;

            // Update individual player stats
            if (allyStats != null)
                allyStats.won = (allyStats.teamId == winnerTeamId);

            if (enemyStats != null)
                enemyStats.won = (enemyStats.teamId == winnerTeamId);

            ResolveMatchups();
            matchActive = false;
        }

        public MatchupStats RegisterMatchup(string characterAId, string characterBId) {

            var matchup = new MatchupStats(
                matchId: matchStats.matchId,
                characterAId: characterAId,
                characterBId: characterBId
            );

            return matchup;
        }

        public void ResolveMatchups() {
            if (matchupStats == null) return;

            // Compare ally vs enemy stats to determine winner
            if (allyStats != null && enemyStats != null) {
                if (allyStats.won && !enemyStats.won)
                    matchupStats.winnerCharacterId = allyStats.characterId;
                else if (enemyStats.won && !allyStats.won)
                    matchupStats.winnerCharacterId = enemyStats.characterId;
                else
                    matchupStats.winnerCharacterId = "tie"; // tie
            }
        }
    }
}

        