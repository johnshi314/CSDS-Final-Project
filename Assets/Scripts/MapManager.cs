/***********************************************************************
* File Name     : MapManager.cs
* Author        : Genevieve Resnik
* Date Created  : 2026-02-01
* Description   : Data structure representing the game map manager
**********************************************************************/
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace NetFlower {
    public class MapManager {

        // Map currently being managed
        public readonly Map ActiveMap;
        public bool HasActiveMap => ActiveMap != null;
        public readonly Team TeamRed;
        public readonly Team TeamBlue;

        public MapManager(
            Team redTeam,
            Team blueTeam,
            string mapName = "New World",
            bool[,] tiles = null,
            IEnumerable<Vector2Int> redSpawnPoints = null,
            IEnumerable<Vector2Int> blueSpawnPoints = null
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

            if (tiles == null) {
                Debug.LogError("MapManager: Tiles data is null. Please provide valid walkability data for the map.");
                return;
            }

            this.TeamRed = redTeam;
            this.TeamBlue = blueTeam;

            List<Vector2Int> rsp = redSpawnPoints?.ToList() ?? new List<Vector2Int>();
            List<Vector2Int> bsp = blueSpawnPoints?.ToList() ?? new List<Vector2Int>();

            // Calculate how many spawn points are needed for each team based on team size and provided spawn points
            int redSpawnsNeeded = this.TeamRed.Members.Count - rsp.Count;
            int blueSpawnsNeeded = this.TeamBlue.Members.Count - bsp.Count;

            // Default separate teams on opposite sides of the map if spawn points are not provided
            if (redSpawnsNeeded > 0) {
                for (int i = 0; i < tiles.GetLength(0); i++) {
                    for (int j = 0; j < tiles.GetLength(1); j++) {
                        if (tiles[i, j]) { // If tile is walkable
                            rsp.Add(new Vector2Int(i, j));
                            if (rsp.Count >= redSpawnsNeeded) break;
                        }
                    }
                    if (rsp.Count >= redSpawnsNeeded) break;
                }
            }
            if (blueSpawnsNeeded > 0) {
                for (int i = tiles.GetLength(0) - 1; i >= 0; i--) {
                    for (int j = tiles.GetLength(1) - 1; j >= 0; j--) {
                        if (tiles[i, j] && !rsp.Contains(new Vector2Int(i, j))) { // If tile is walkable and not already a spawn point
                            bsp.Add(new Vector2Int(i, j));
                            if (bsp.Count >= blueSpawnsNeeded) break;
                        }
                    }
                    if (bsp.Count >= blueSpawnsNeeded) break;
                }
            }
            List<Vector2Int> combinedSpawnPoints = new List<Vector2Int>();
            combinedSpawnPoints.AddRange(rsp);
            combinedSpawnPoints.AddRange(bsp);

            this.ActiveMap = new Map(
                mapName,
                tiles,
                rsp.ToArray(),
                bsp.ToArray()
            );
            
             // Initialize map with agents
             List<Agent> initialAgents = new List<Agent>();
             initialAgents.AddRange(this.TeamRed.Members);
             initialAgents.AddRange(this.TeamBlue.Members);

            // Register Red Team agents
            List<Agent> redMembers = new List<Agent>(this.TeamRed.Members);
            for (int i = 0; i < Mathf.Min(rsp.Count, redMembers.Count); i++) {
                Vector2Int spawnPoint = rsp[i];
                Agent agent = redMembers[i];
                if (agent != null && !PlaceAgent(agent, spawnPoint)) {
                    Debug.LogWarning($"MapManager: Failed to place {agent.Name} at {spawnPoint}.");
                }
            }

            // Register Blue Team agents
            List<Agent> blueMembers = new List<Agent>(this.TeamBlue.Members);
            for (int i = 0; i < Mathf.Min(bsp.Count, blueMembers.Count); i++) {
                Vector2Int spawnPoint = bsp[i];
                Agent agent = blueMembers[i];
                if (agent != null && !PlaceAgent(agent, spawnPoint)) {
                    Debug.LogWarning($"MapManager: Failed to place {agent.Name} at {spawnPoint}.");
                }
            }
        }

        public MapManager(Team redTeam, Team blueTeam, Map map) : this(redTeam, blueTeam, map.MapName, map.GetWalkability(), map.RedSpawnPoints, map.BlueSpawnPoints) {}

        // ===================================================================== //
        // ======================= Agent Placement ============================= //

        /// <summary>
        /// Place an agent on a specific tile and register them on the map
        /// </summary>
        /// <param name="agent">The agent to place.</param>
        /// <param name="tilePos">The tile to place the agent on.</param>
        /// <returns>True if the agent was placed successfully, false otherwise.</returns>
        public bool PlaceAgent(Agent agent, Vector2Int tilePos) {
            // Check if we can place the agent at the target position
            if (!CanPlaceAgent(agent, tilePos)) {
                string agentName = agent != null ? agent.Name : "<null>";
                Debug.LogWarning($"MapManager: Cannot place {agentName} at {tilePos}. Position is out of bounds, not walkable, or already occupied.");
                return false;
            }

            // Register agent on Map
            ActiveMap.RegisterAgent(agent, tilePos);

            return true;
        }

        // ===================================================================== //
        // ======================= Movement Requests =========================== //
        /// <summary>
        /// Request to move an agent to a target tile. Checks if the move is valid (within bounds, walkable, not occupied, and agent is registered) before updating the agent's position on the map.
        /// </summary>
        /// <param name="agent">The agent to move.</param>
        /// <param name="targetTile">The tile to move the agent to.</param>
        /// <returns>True if the move is valid and executed, false otherwise.</returns>
        public bool RequestMove(Agent agent, Vector2Int targetTile) {
            // Check if move is valid
            if (!CanMoveAgent(agent, targetTile)) {
                string agentName = agent != null ? agent.Name : "<null>";
                Debug.LogWarning($"MapManager: Cannot move {agentName} to {targetTile}. Position is out of bounds, not walkable, already occupied, or agent is not registered.");
                return false;
            }

            // Update agent position in Map
            ActiveMap.MoveAgent(agent, targetTile);

            return true;
        }
        /// <summary>
        /// Checks if an agent can move to a target tile. Validates that the target tile is within bounds, walkable, not occupied by another agent (unless it's the same agent), and that the agent is currently registered on the map. This method does not execute the move, it only checks its validity.
        /// </summary>
        /// <param name="agent">The agent to move.</param>
        /// <param name="tilePos">The tile to move the agent to.</param>
        /// <returns>True if the move is valid, false otherwise.</returns>
        public bool CanMoveAgent(Agent agent, Vector2Int tilePos) {
            return CanPlaceAgent(agent, tilePos) && IsRegistered(agent);
        }

        // ===================================================================== //
        // ======================= Helper Methods ============================== //
        private bool CanPlaceAgent(Agent agent, Vector2Int tilePos) {
            if (!HasActiveMap || agent == null) return false;

            // Check bounds
            if (!ActiveMap.InBounds(tilePos)) return false;

            // Check walkability
            if (!ActiveMap.Tiles[tilePos.x, tilePos.y].IsWalkable) return false;

            // Check occupancy
            Agent occupant = ActiveMap.GetAgentAtPosition(tilePos);
            if (occupant != null && occupant != agent) return false;

            return true;
        }

        private bool IsRegistered(Agent agent) {
            if (!HasActiveMap || agent == null) return false;
            return ActiveMap.GetCurrentTile(agent) != null;
        }
    }
}
