/***********************************************************************
* File Name     : MapManager.cs
* Author        : Genevieve Resnik
* Date Created  : 2026-02-01
* Description   : Data structure representing the game map manager
**********************************************************************/
using UnityEngine;
using System.Collections.Generic;

namespace NetFlower {
    public class MapManager {

        // Map currently being managed
        private Map activeMap;

        private string mapName = "New World";
        private List<Vector2Int> redSpawnPoints = new List<Vector2Int>();
        private List<Vector2Int> blueSpawnPoints = new List<Vector2Int>();
        public List<Vector2Int> RedSpawnPoints => redSpawnPoints;
        public List<Vector2Int> BlueSpawnPoints => blueSpawnPoints;
        public Map Map => activeMap;
        public string MapName => mapName;
        public bool HasActiveMap => activeMap != null;
        public int MapWidth => activeMap != null ? activeMap.Width : 0;
        public int MapHeight => activeMap != null ? activeMap.Height : 0;
        
        // Agents to initialize (For assigning in editor, use GridMap Component)
        private Team redTeam;
        private Team blueTeam;
        
        // ===================================================================== //
        // ======================= Initialization Method ======================= //

        public MapManager(
            string mapName,
            bool[,] tiles,
            Team redTeam,
            Team blueTeam,
            List<Vector2Int> redSpawnPoints = null,
            List<Vector2Int> blueSpawnPoints = null
        ) {

            // Throw error if either redTeam or blueTeam is null or has no members
            if (redTeam == null || redTeam.Members.Count == 0) {
                Debug.LogError("MapManager: Red team is null or has no members. Please provide a valid team with agents.");
                return;
            }
            if (blueTeam == null || blueTeam.Members.Count == 0) {
                Debug.LogError("MapManager: Blue team is null or has no members. Please provide a valid team with agents.");
                return;
            }

            this.redTeam = redTeam;
            this.blueTeam = blueTeam;

            this.redSpawnPoints = redSpawnPoints ?? new List<Vector2Int>();
            this.blueSpawnPoints = blueSpawnPoints ?? new List<Vector2Int>();
            
            // Calculate how many spawn points are needed for each team based on team size and provided spawn points
            int redSpawnsNeeded = this.redTeam.Members.Count - this.redSpawnPoints.Count;
            int blueSpawnsNeeded = this.blueTeam.Members.Count - this.blueSpawnPoints.Count;

            // Default separate teams on opposite sides of the map if spawn points are not provided
            if (redSpawnsNeeded > 0) {
                for (int i = 0; i < tiles.GetLength(0); i++) {
                    for (int j = 0; j < tiles.GetLength(1); j++) {
                        if (tiles[i, j]) { // If tile is walkable
                            this.redSpawnPoints.Add(new Vector2Int(i, j));
                            if (this.redSpawnPoints.Count >= redSpawnsNeeded) break;
                        }
                    }
                    if (this.redSpawnPoints.Count >= redSpawnsNeeded) break;
                }
            }
            if (blueSpawnsNeeded > 0) {
                for (int i = tiles.GetLength(0) - 1; i >= 0; i--) {
                    for (int j = tiles.GetLength(1) - 1; j >= 0; j--) {
                        if (tiles[i, j] && !this.redSpawnPoints.Contains(new Vector2Int(i, j))) { // If tile is walkable and not already a spawn point
                            this.blueSpawnPoints.Add(new Vector2Int(i, j));
                            if (this.blueSpawnPoints.Count >= blueSpawnsNeeded) break;
                        }
                    }
                    if (this.blueSpawnPoints.Count >= blueSpawnsNeeded) break;
                }
            }
            List<Vector2Int> combinedSpawnPoints = new List<Vector2Int>();
            combinedSpawnPoints.AddRange(this.redSpawnPoints);
            combinedSpawnPoints.AddRange(this.blueSpawnPoints);

            this.activeMap = new Map(
                mapName,
                tiles,
                combinedSpawnPoints.ToArray()
            );
            
             // Initialize map with agents
             List<Agent> initialAgents = new List<Agent>();
             initialAgents.AddRange(this.redTeam.Members);
             initialAgents.AddRange(this.blueTeam.Members);

            // Register Red Team agents
            List<Agent> redMembers = new List<Agent>(this.redTeam.Members);
            for (int i = 0; i < Mathf.Min(this.redSpawnPoints.Count, redMembers.Count); i++) {
                Vector2Int spawnPoint = this.redSpawnPoints[i];
                Agent agent = redMembers[i];
                if (agent != null && !PlaceAgent(agent, spawnPoint)) {
                    Debug.LogWarning($"MapManager: Failed to place {agent.Name} at {spawnPoint}.");
                }
            }

            // Register Blue Team agents
            List<Agent> blueMembers = new List<Agent>(this.blueTeam.Members);
            for (int i = 0; i < Mathf.Min(this.blueSpawnPoints.Count, blueMembers.Count); i++) {
                Vector2Int spawnPoint = this.blueSpawnPoints[i];
                Agent agent = blueMembers[i];
                if (agent != null && !PlaceAgent(agent, spawnPoint)) {
                    Debug.LogWarning($"MapManager: Failed to place {agent.Name} at {spawnPoint}.");
                }
            }
        }

        // ===================================================================== //
        // ======================= Agent Placement ============================= //

        // Place an agent on a specific tile and register them on the map
        public bool PlaceAgent(Agent agent, Vector2Int tilePos) {
            // Check if we can place the agent at the target position
            if (!CanPlaceAgent(agent, tilePos)) {
                string agentName = agent != null ? agent.Name : "<null>";
                Debug.LogWarning($"MapManager: Cannot place {agentName} at {tilePos}. Position is out of bounds, not walkable, or already occupied.");
                return false;
            }

            // Register agent on Map
            activeMap.RegisterAgent(agent, tilePos);

            return true;
        }

        // ===================================================================== //
        // ======================= Movement Requests =========================== //
        public bool RequestMove(Agent agent, Vector2Int targetTile) {
            // Check if move is valid
            if (!CanMoveAgent(agent, targetTile)) {
                string agentName = agent != null ? agent.Name : "<null>";
                Debug.LogWarning($"MapManager: Cannot move {agentName} to {targetTile}. Position is out of bounds, not walkable, already occupied, or agent is not registered.");
                return false;
            }

            // Update agent position in Map
            activeMap.MoveAgent(agent, targetTile);

            return true;
        }

        // ===================================================================== //
        // ======================= Helper Methods ============================== //
        private bool CanPlaceAgent(Agent agent, Vector2Int tilePos) {
            if (!HasActiveMap || agent == null) return false;

            // Check bounds
            if (!activeMap.InBounds(tilePos)) return false;

            // Check walkability
            if (!activeMap.Tiles[tilePos.x, tilePos.y].IsWalkable) return false;

            // Check occupancy
            Agent occupant = activeMap.GetAgentAtPosition(tilePos);
            if (occupant != null && occupant != agent) return false;

            return true;
        }

        public bool CanMoveAgent(Agent agent, Vector2Int tilePos) {
            if (!HasActiveMap || agent == null) return false;

            // Agent must already be registered to move
            if (activeMap.GetCurrentTile(agent) == null) return false;

            // Check bounds
            if (!activeMap.InBounds(tilePos)) return false;

            // Check walkability
            if (!activeMap.Tiles[tilePos.x, tilePos.y].IsWalkable) return false;

            // Check occupancy
            Agent occupant = activeMap.GetAgentAtPosition(tilePos);
            if (occupant != null && occupant != agent) return false;

            return true;
        }
    }
}
