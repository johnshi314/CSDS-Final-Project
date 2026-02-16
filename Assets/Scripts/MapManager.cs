/***********************************************************************
* File Name     : MapManager.cs
* Author        : Genevieve Resnik
* Date Created  : 2026-02-01
* Description   : Data structure representing the game map manager
**********************************************************************/
using UnityEngine;
using System.Collections.Generic;
using NetFlower;

namespace NetFlower {
    public class MapManager : MonoBehaviour {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start() { }

        // Update is called once per frame
        void Update() { }

        // Map currently being managed
        public Map ActiveMap;
        
        // Agents to initialize (For assigning in editor, use GridMap Component)
        private List<Agent> initialAgents = new List<Agent>();
        
        // Runtime occupancy tracking (fast lookups)
        private Dictionary<Vector2Int, Agent> occupants = new Dictionary<Vector2Int, Agent>();

        // ===================================================================== //
        // ======================= Initialization Method ======================= //

        /// <summary>
        /// Set the initial agents to be placed during initialization.
        /// Call this before Initialize() to override the inspector values.
        /// 
        /// Currently used in GridMap.cs so it can use their own list of
        /// agents and will override MapManager's inspector values
        /// This is so GridMap can properly move the GameObjects of the agents
        /// in the scene to match their positions on the map.
        /// </summary>
        public void SetInitialAgents(List<Agent> agents) {
            initialAgents = agents != null ? new List<Agent>(agents) : new List<Agent>();
        }

        public void Initialize(Map map) {
            this.ActiveMap = map;
            occupants.Clear();
            
            // Check if there are enough starting positions for all agents
            if (map.SpawnPoints.Length < initialAgents.Count) {
                Debug.LogWarning($"MapManager: Not enough spawn points for all agents! Map has {map.SpawnPoints.Length} spawn points but {initialAgents.Count} agents.");
            }

            // Register agents up to the number of spawn points available
            for (int i = 0; i < Mathf.Min(map.SpawnPoints.Length, initialAgents.Count); i++) {
                Vector2Int spawnPoint = map.SpawnPoints[i];
                Agent agent = initialAgents[i];
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
                Debug.LogWarning($"MapManager: Cannot place {agent.Name} at {tilePos}. Position is out of bounds, not walkable, or already occupied.");
                return false;
            }

            // Place agent in occupancy dictionary
            occupants[tilePos] = agent;

            // Register agent on Map
            ActiveMap.RegisterAgent(agent, tilePos);

            return true;
        }

        // ===================================================================== //
        // ======================= Movement Requests =========================== //
        public bool RequestMove(Agent agent, Vector2Int targetTile) {
            // Check if move is valid
            if (!CanPlaceAgent(agent, targetTile)) {
                Debug.LogWarning($"MapManager: Cannot move {agent.Name} to {targetTile}. Position is out of bounds, not walkable, or already occupied.");
                return false;
            }

            // Find current tile of agent
            Tile currentTile = ActiveMap.GetCurrentTile(agent);

            if (currentTile != null) {
                // Clear old position from occupancy dictionary
                occupants.Remove(currentTile.Position);
            }

            // Place agent on new tile
            occupants[targetTile] = agent;

            // Update agent position in Map
            ActiveMap.MoveAgent(agent, targetTile);

            return true;
        }

        // ===================================================================== //
        // ======================= Helper Methods ============================== //
        public bool CanPlaceAgent(Agent agent, Vector2Int tilePos) {
            // Check bounds
            if (!InBounds(tilePos)) return false;

            // Check walkability
            if (!ActiveMap.Tiles[tilePos.x, tilePos.y].IsWalkable) return false;

            // Check occupancy
            if (occupants.ContainsKey(tilePos)) return false;

            return true;
        }

        // Check if position in bounds
        private bool InBounds(Vector2Int tilePos) {
            return tilePos.x >= 0 && tilePos.x < ActiveMap.Width &&
            tilePos.y >= 0 && tilePos.y < ActiveMap.Height;
        }

        // Check if tile is occupied by any agent
        public bool IsOccupied(Vector2Int tilePos) {
            if (!InBounds(tilePos)) return true;
            return occupants.ContainsKey(tilePos);
        }
        
        // Get the agent at a specific position
        public Agent GetAgentAt(Vector2Int tilePos) {
            return occupants.TryGetValue(tilePos, out Agent agent) ? agent : null;
        }
     }
  }
