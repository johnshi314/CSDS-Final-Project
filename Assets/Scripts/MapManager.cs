/***********************************************************************
* File Name     : MapManager.cs
* Author        : Genevieve Resnik
* Date Created  : 2026-02-01
* Description   : Data structure representing the game map manager
**********************************************************************/
using UnityEngine;
using System.Collections.Generic;

namespace NetFlower {
    public class MapManager : MonoBehaviour {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start() { }

        // Update is called once per frame
        void Update() { }

        // Map currently being managed
        private Map activeMap;

        [Header("Map Configuration")]
        [SerializeField] private string mapName = "New World";
        [SerializeField] private Vector2Int[] spawnPoints = new Vector2Int[0];

        public Map Map => activeMap;
        public string MapName => mapName;
        public Vector2Int[] SpawnPoints => spawnPoints;
        public bool HasActiveMap => activeMap != null;
        public int MapWidth => activeMap != null ? activeMap.Width : 0;
        public int MapHeight => activeMap != null ? activeMap.Height : 0;
        
        // Agents to initialize (For assigning in editor, use GridMap Component)
        private List<Agent> initialAgents = new List<Agent>();
        
        // ===================================================================== //
        // ======================= Initialization Method ======================= //

        public void Initialize(Map map, List<Agent> agents) {
            this.activeMap = map;
            this.initialAgents = agents ?? new List<Agent>();
            
            if (spawnPoints == null) {
                spawnPoints = new Vector2Int[0];
            }
            
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
