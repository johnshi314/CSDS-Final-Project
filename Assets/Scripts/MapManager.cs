/***********************************************************************
* File Name     : MapManager.cs
* Author        : Genevieve Resnik
* Date Created  : 2026-02-01
* Description   : Data structure representing the game map manager
**********************************************************************/
using UnityEngine;
using NetFlower;

namespace NetFlower {
    public class MapManager : MonoBehaviour {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start() { }

        // Update is called once per frame
        void Update() { }

        // Map currently being managed
        public Map ActiveMap;
        // Track occupants on map
        public Agent[,] Occupants;

        // ===================================================================== //
        // ======================= Initialization Method ======================= //

        public void Initialize(Map map) {
            this.ActiveMap = map;
            Occupants = new Agent[map.Width, map.Height];
        }

        // ===================================================================== //
        // ======================= Agent Placement ============================= //

        // Place an agent on a specific tile and register them on the map
        public bool PlaceAgent(Agent agent, Vector2Int tilePos) {

            // Check bounds
            if (!InBounds(tilePos)) return false;

            // Check walkability
            if (!ActiveMap.Tiles[tilePos.x, tilePos.y].IsWalkable) return false;

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
            if (!ActiveMap.Tiles[targetTile.x, targetTile.y].IsWalkable) return false;

            // Check occupancy
            if (Occupants[targetTile.x, targetTile.y] != null) return false;

            // Find current tile of agent
            Tile currentTile = ActiveMap.GetCurrentTile(agent);

            if (currentTile != null) {
                // Clear old position
                Occupants[currentTile.Position.x, currentTile.Position.y] = null;
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
            return tilePos.x >= 0 && tilePos.x < ActiveMap.Width &&
            tilePos.y >= 0 && tilePos.y < ActiveMap.Height;
        }

        // Check if tile is occupied by any agent
        public bool IsOccupied(Vector2Int tilePos) {
            if (!InBounds(tilePos)) return true;
            return Occupants[tilePos.x, tilePos.y] != null;
        }
     }
  }
