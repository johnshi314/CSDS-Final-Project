/***********************************************************************
* File Name     : Map.cs
* Author        : Genevieve Resnik
* Date Created  : 2026-02-01
* Description   : Data structure representing the game map
**********************************************************************/
using UnityEngine;
using System.Collections.Generic;

namespace GameData {
    public class Map : MonoBehaviour {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start() { }

        // Update is called once per frame
        void Update() { }

        // Meta Information
        public string mapName { get; private set; }

        // Tile Data (walkable or blocked)
        public bool[,] tiles { get; private set; }
        public int width { get; private set; } = 10;
        public int height { get; private set; } = 10;

        // Tile indices that are valid spawnpoints
        public Vector2Int[] spawnPoints { get; private set; }

        // Player Tracking
        // Track each agent's start tile
        private Dictionary<Agent, Vector2Int> startTiles = new Dictionary<Agent, Vector2Int>();

        // Track each agent's current tile
        private Dictionary<Agent, Vector2Int> currentTiles = new Dictionary<Agent, Vector2Int>();

        // Track each agent's full path
        private Dictionary<Agent, List<Vector2Int>> paths = new Dictionary<Agent, List<Vector2Int>>();

        // ===================================================================== //
        // ======================= Static Factory Method ======================= //

        /// <summary>
        /// Factory method to create a new Map GameObject with specified properties.
        /// </summary>
        /// <param name="mapName">A unique identifier for the map.</param>
        /// <param name="tiles">Stores which tiles on the map is walkable and blocked.</param>
        /// <param name="spawnPoints">Valid spawnpoint indices on the map.</param>
        /// </summary>

        public static Map NewMap(
        string mapName,
        bool[,] tiles,
        Vector2Int[] spawnPoints,
        GameObject parent = null) {

            // Create Map GameObject
            GameObject mapObject = new GameObject(mapName);

            // Add map component and nitialize data
            Map map = mapObject.AddComponent<Map>();
            map.mapName = mapName;
            map.tiles = tiles;
            map.spawnPoints = spawnPoints;
            return map;
        }
        // ===================================================================== //
        // ======================= Public Map Methods ======================== //

        // Register agent with starting tile
        public void RegisterAgent(Agent agent, Vector2Int startTile) {
            startTiles[agent] = startTile;
            currentTiles[agent] = startTile;

            if (!paths.ContainsKey(agent)) {
                paths[agent] = new List<Vector2Int>();
            }
            paths[agent].Add(startTile);
        }

        // Move player to a new tile index
        // Map Manager will validate move
        public void MoveAgent(Agent agent, Vector2Int tilePos) {
            if (!currentTiles.ContainsKey(agent)) return;
            currentTiles[agent] = tilePos;
            paths[agent].Add(tilePos);
        }

        // Get starting tile of agent
        public Vector2Int GetStartTile(Agent agent) {
            return startTiles.ContainsKey(agent) ? startTiles[agent] : new Vector2Int(-1, -1);
        }

        // Get current tile of agent
        public Vector2Int GetCurrentTile(Agent agent) {
            return currentTiles.ContainsKey(agent) ? currentTiles[agent] : new Vector2Int(-1, -1);
        }

        // Get full path of an agent
        public List<Vector2Int> GetPath(Agent agent) {
            return paths.ContainsKey(agent) ? paths[agent] : new List<Vector2Int>();
        }
    }
}
