/***********************************************************************
* File Name     : Map.cs
* Author        : Genevieve Resnik
* Date Created  : 2026-02-01
* Description   : Data structure representing the game map
**********************************************************************/
using UnityEngine;
using System.Collections.Generic;
using GameData;

namespace GameMap {
    public class Map : MonoBehaviour {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start() { }

        // Update is called once per frame
        void Update() { }

        // Meta Information
        public string MapName;

        // Tile Data (walkable or blocked)
        public Tile[,] Tiles;
        public int Width { 
            get {
                if (Tiles != null)
                {
                    return Tiles.GetLength(0);
                }
                return 0;
            }}
        public int Height { 
            get {
                if (Tiles != null)
                {
                    return Tiles.GetLength(1);
                }
                return 0;
            }}

        // Tile indices that are valid spawnpoints
        public Vector2Int[] SpawnPoints;

        // Player Tracking
        // Track each agent's start tile
        private Dictionary<Agent, Tile> startTiles = new Dictionary<Agent, Tile>();

        // Track each agent's current tile
        private Dictionary<Agent, Tile> currentTiles = new Dictionary<Agent, Tile>();

        // Track each agent's full path
        private Dictionary<Agent, List<Tile>> paths = new Dictionary<Agent, List<Tile>>();

        // ===================================================================== //
        // ======================= Static Factory Method ======================= //

        /// <summary>
        /// Factory method to create a new Map GameObject with specified properties.
        /// </summary>
        /// <param name="mapName">A unique identifier for the map.</param>
        /// <param name="tiles">Stores which tiles on the map is walkable and blocked.</param>
        /// <param name="spawnPoints">Valid spawnpoint indices on the map.</param>
        public static GameObject NewMap(
        string mapName,
        bool[,] tiles,
        Vector2Int[] spawnPoints,
        GameObject parent = null) {

            // Create Map GameObject
            GameObject mapObject = new GameObject(mapName);

            if (parent != null) {
                mapObject.transform.SetParent(parent.transform);
            }

            // Create Map
            Map map = mapObject.AddComponent<Map>();
            map.Initialize(mapName, tiles, spawnPoints);

            return mapObject;
        }

        /// <summary>
        /// Initialize a map with the given properties.
        /// </summary>
        /// <param name="mapName"></param>
        /// <param name="tiles"></param>
        /// <param name="spawnPoints"></param>
        public void Initialize(
        string mapName,
        bool[,] tiles,
        Vector2Int[] spawnPoints) {
            // Assert spawn points are within bounds of the tile array
            int width = tiles.GetLength(0);
            int height = tiles.GetLength(1);
            foreach (Vector2Int pos in spawnPoints) {
                if (pos.x < 0 || pos.x >= width || pos.y < 0 || pos.y >= height) {
                    Debug.LogError($"Spawn point {pos} is out of bounds for tile array of size ({width}, {height}).");
                    return;
                }
            }
            this.SpawnPoints = spawnPoints;

            // Set map name
            this.MapName = mapName;
            
            // Build the tile array
            this.Tiles = new Tile[width, height];
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < height; y++) {
                    this.Tiles[x, y] = new Tile(this, new Vector2Int(x, y), tiles[x, y]);
                }
            }
        }
        // ===================================================================== //
        // ======================= Public Map Methods ======================== //

        // Register agent with starting tile
        public void RegisterAgent(Agent agent, Vector2Int startPos) {
            Tile startTile = this.Tiles[startPos.x, startPos.y];
            startTiles[agent] = startTile;
            currentTiles[agent] = startTile;

            if (!paths.ContainsKey(agent)) {
                paths[agent] = new List<Tile>();
            }
            paths[agent].Add(startTile);
        }

        // Move player to a new tile index
        // Map Manager will validate move
        public void MoveAgent(Agent agent, Vector2Int tilePos) {
            if (!currentTiles.ContainsKey(agent)) return;
            Tile movedToTile = this.Tiles[tilePos.x, tilePos.y];
            currentTiles[agent] = movedToTile;
            paths[agent].Add(movedToTile);
        }

        // Get starting tile of agent
        public Tile GetStartTile(Agent agent) {
            return startTiles.ContainsKey(agent) ? startTiles[agent] : null;
        }

        // Get current tile of agent
        public Tile GetCurrentTile(Agent agent) {
            return currentTiles.ContainsKey(agent) ? currentTiles[agent] : null;
        }

        // Get full path of an agent
        public List<Tile> GetPath(Agent agent) {
            return paths.ContainsKey(agent) ? paths[agent] : new List<Tile>();
        }

        // Get the agent at a specific tile, if any
        public Agent GetAgentAtTile(Tile tile) {
            foreach (var kvp in currentTiles) {
                if (kvp.Value.Position == tile.Position) {
                    return kvp.Key;
                }
            }
            return null;
        }

        // Get agent at a specific tile position, if any
        public Agent GetAgentAtPosition(Vector2Int tilePos) {
            int x = tilePos.x;
            int y = tilePos.y;
            if (x < 0 || x >= Width || y < 0 || y >= Height) {
                Debug.LogError($"Tile position {tilePos} is out of bounds for tile array of size ({Width}, {Height}).");
                return null;
            }
            return GetAgentAtTile(this.Tiles[x, y]);
        }

        public Tile GetTileAtPosition(Vector2Int tilePos) {
            int x = tilePos.x;
            int y = tilePos.y;
            if (x < 0 || x >= Width || y < 0 || y >= Height) {
                Debug.LogError($"Tile position {tilePos} is out of bounds for tile array of size ({Width}, {Height}).");
                return null;
            }
            return this.Tiles[x, y];
        }
    }
}
