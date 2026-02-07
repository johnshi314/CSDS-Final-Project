/***********************************************************************
* File Name     : MapManager.cs
* Author        : Genevieve Resnik
* Date Created  : 2026-02-01
* Description   : Data structure representing the game map manager
**********************************************************************/
using UnityEngine;
using GameMap;
using GameData;

namespace GameManager {
    public class MapManager : MonoBehaviour {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start() { }

        // Update is called once per frame
        void Update() { }

        // Map currently being managed
        public Map ActiveMap { get; private set; }

        // Track occupants on map
        public Agent[,] Occupants { get; private set; }

        // ===================================================================== //
        // ======================= Initialization Method ======================= //

        public void Initialize(Map map) {
            this.ActiveMap = map;
            Occupants = new Agent[map.width, map.height];
        }

        // ===================================================================== //
        // ======================= Agent Placement ============================= //

        // Place an agent on a specific tile and register them on the map
        public bool PlaceAgent(Agent agent, Vector2Int tilePos) {

            // Check bounds
            if (!InBounds(tilePos)) return false;

            // Check walkability
            if (!ActiveMap.tiles[tilePos.x, tilePos.y]) return false;

            // Check occupancy
            if (Occupants[tilePos.x, tilePos.y] != null) return false;

            // Place agent
            Occupants[tilePos.x, tilePos.y] = agent;

            // Register agent on Map
            ActiveMap.RegisterAgent(agent, tilePos);

            return true;
        }

        // ===================================================================== //
        // ======================= Movement Requests =========================== //
        public bool RequestMove(Agent agent, Vector2Int targetTile) {
            // Check bounds
            if (!InBounds(targetTile)) return false;

            // Check walkability
            if (!ActiveMap.tiles[targetTile.x, targetTile.y]) return false;

            // Check occupancy
            if (Occupants[targetTile.x, targetTile.y] != null) return false;

            // Find current tile of agent
            Vector2Int currentTile = ActiveMap.GetCurrentTile(agent);

            if (currentTile != new Vector2Int(-1, -1)) {
                // Clear old position
                Occupants[currentTile.x, currentTile.y] = null;
            }

            // Place agent on new tile
            Occupants[targetTile.x, targetTile.y] = agent;

            // Update agent position in Map
            ActiveMap.MoveAgent(agent, targetTile);

            return true;
        }

            // ===================================================================== //
            // ======================= Helper Methods ============================== //

        // Check if position in bounds
        private bool InBounds(Vector2Int tilePos) {
            return tilePos.x >= 0 && tilePos.x < ActiveMap.width &&
            tilePos.y >= 0 && tilePos.y < ActiveMap.height;
        }

        // Check if tile is occupied by any agent
        public bool IsOccupied(Vector2Int tilePos) {
            if (!InBounds(tilePos)) return true;
            return Occupants[tilePos.x, tilePos.y] != null;
        }
     }
  }
