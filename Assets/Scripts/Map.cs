/***********************************************************************
* File Name     : Map.cs
* Author        : Genevieve Resnik
* Date Created  : 2026-02-01
* Description   : Data structure representing the game map
**********************************************************************/
using UnityEngine;
using System.Collections.Generic;

namespace NetFlower {
    public class Map {

        // Meta Information
        public readonly string MapName;

        // Tile Data (walkable or blocked)
        public readonly Tile[,] Tiles;
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

        // Tile indices that are valid spawnpoints. Assigned using GridMap Component.
        private List<Vector2Int> redSpawnPoints;
        private List<Vector2Int> blueSpawnPoints;
        public IReadOnlyList<Vector2Int> RedSpawnPoints => redSpawnPoints;
        public IReadOnlyList<Vector2Int> BlueSpawnPoints => blueSpawnPoints;

        // Player Tracking
        // Track each agent's start tile
        private Dictionary<Agent, Tile> startTiles = new Dictionary<Agent, Tile>();

        // Track each agent's current tile
        private Dictionary<Agent, Tile> currentTiles = new Dictionary<Agent, Tile>();

        // Reverse lookup: current occupant by tile position
        private Dictionary<Vector2Int, Agent> agentsByPosition = new Dictionary<Vector2Int, Agent>();

        // Track each agent's full path
        private Dictionary<Agent, List<Tile>> paths = new Dictionary<Agent, List<Tile>>();

        // ===================================================================== //
        // ============================ Constructor ============================ //
        /// <summary>
        /// Factory method to create a new Map GameObject with specified properties.
        /// </summary>
        /// <param name="mapName">A unique identifier for the map.</param>
        /// <param name="tiles">Stores which tiles on the map is walkable and blocked.</param>
        /// <param name="redSpawnPoints">Valid spawnpoint indices for the red team.</param>
        /// <param name="blueSpawnPoints">Valid spawnpoint indices for the blue team.</param>
        public Map(string mapName,
                bool[,] tiles = null,
                IEnumerable<Vector2Int> redSpawnPoints = null,
                IEnumerable<Vector2Int> blueSpawnPoints = null) {
            if (tiles == null) {
                tiles = new bool[0, 0];
            }
            List<Vector2Int> redSpawnPointList = redSpawnPoints == null ? new List<Vector2Int>() : new List<Vector2Int>(redSpawnPoints);
            List<Vector2Int> blueSpawnPointList     = blueSpawnPoints == null ? new List<Vector2Int>() : new List<Vector2Int>(blueSpawnPoints);
            if (string.IsNullOrEmpty(mapName)) {
                mapName = "New World";
            }
            // Assert spawn points are within bounds of the tile array
            int width = tiles.GetLength(0);
            int height = tiles.GetLength(1);
            foreach (Vector2Int pos in redSpawnPointList) {
                if (pos.x < 0 || pos.x >= width || pos.y < 0 || pos.y >= height) {
                    Debug.LogError($"Red spawn point {pos} is out of bounds for tile array of size ({width}, {height}).");
                    return;
                }
            }
            foreach (Vector2Int pos in blueSpawnPointList) {
                if (pos.x < 0 || pos.x >= width || pos.y < 0 || pos.y >= height) {
                    Debug.LogError($"Blue spawn point {pos} is out of bounds for tile array of size ({width}, {height}).");
                    return;
                }
            }
            this.redSpawnPoints = redSpawnPointList;
            this.blueSpawnPoints = blueSpawnPointList;

            // Set map name
            this.MapName = mapName;
            
            // Build the tile array
            this.Tiles = new Tile[width, height];
            for (int x = 0; x < width; x++) {
                for (int y = 0; y < height; y++) {
                    this.Tiles[x, y] = new Tile(this, new Vector2Int(x, y), tiles[x, y]);
                }
            }

            // Reset tracking structures for fresh map state
            startTiles.Clear();
            currentTiles.Clear();
            agentsByPosition.Clear();
            paths.Clear();
        }
        // ===================================================================== //
        // ======================= Public Map Methods ======================== //

        // Register agent with starting tile
        public void RegisterAgent(Agent agent, Vector2Int startPos) {
            Tile startTile = this.Tiles[startPos.x, startPos.y];

            // Remove previous occupancy if re-registering same agent
            if (currentTiles.TryGetValue(agent, out Tile previousTile)) {
                agentsByPosition.Remove(previousTile.Position);
            }

            startTiles[agent] = startTile;
            currentTiles[agent] = startTile;
            agentsByPosition[startTile.Position] = agent;

            if (!paths.ContainsKey(agent)) {
                paths[agent] = new List<Tile>();
            }
            paths[agent].Add(startTile);
        }

        // Move player to a new tile index
        // Map Manager will validate move
        public void MoveAgent(Agent agent, Vector2Int tilePos) {
            if (!currentTiles.ContainsKey(agent)) return;

            Tile previousTile = currentTiles[agent];
            agentsByPosition.Remove(previousTile.Position);

            Tile movedToTile = this.Tiles[tilePos.x, tilePos.y];
            currentTiles[agent] = movedToTile;
            agentsByPosition[movedToTile.Position] = agent;
            paths[agent].Add(movedToTile);
        }

        public bool InBounds(Vector2Int tilePos) {
            return tilePos.x >= 0 && tilePos.x < this.Width && tilePos.y >= 0 && tilePos.y < this.Height;
        }

        public bool IsWalkable(Vector2Int tilePos) {
            if (!InBounds(tilePos)) return false;
            return this.Tiles[tilePos.x, tilePos.y].IsWalkable;
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
            if (tile == null) return null;
            return agentsByPosition.TryGetValue(tile.Position, out Agent agent) ? agent : null;
        }

        // Get agent at a specific tile position, if any
        public Agent GetAgentAtPosition(Vector2Int tilePos) {
            int x = tilePos.x;
            int y = tilePos.y;
            if (x < 0 || x >= Width || y < 0 || y >= Height) {
                Debug.LogError($"Tile position {tilePos} is out of bounds for tile array of size ({Width}, {Height}).");
                return null;
            }
            return agentsByPosition.TryGetValue(tilePos, out Agent agent) ? agent : null;
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

        /// <summary>
        /// Calculates and returns all tile positions the agent can move to.
        /// Uses Algorithm-based distance calculation based on agent's movement range and walkability of tiles.
        /// </summary>
        public List<Tile> GetMovableTiles(Agent agent) {
            List<Tile> movableTiles = new List<Tile>();
            // TODO: Implement pathfinding-based movement range calculation that accounts for walkability and obstacles.

            return movableTiles;
        }

        /// <summary>
        /// Returns the dimensions of the map as a Vector2Int (width, height).
        /// </summary>
         public Vector2Int GetDimensions() {
            return new Vector2Int(this.Width, this.Height);
         }

        /// <summary>
        /// Returns a 2D array of bools representing the walkability of each tile on the map.
        /// True indicates walkable, false indicates blocked.
        /// </summary>
         public bool[,] GetWalkability() {
            bool[,] walkability = new bool[this.Width, this.Height];
            for (int x = 0; x < this.Width; x++) {
                for (int y = 0; y < this.Height; y++) {
                    walkability[x, y] = this.Tiles[x, y].IsWalkable;
                }
            }
            return walkability;
         }

         public Tile TryGetFirstAvailableSpawnPoint(TeamColor team) {
            var spawnPoints = team == TeamColor.Red ? this.redSpawnPoints : this.blueSpawnPoints;
            foreach (Vector2Int pos in spawnPoints) {
                if (IsWalkable(pos) && GetAgentAtPosition(pos) == null) {
                    return GetTileAtPosition(pos);
                }
            }
            return null;
         }

         public Tile TryGetFirstWalkableTile() {
            for (int x = 0; x < this.Width; x++) {
                for (int y = 0; y < this.Height; y++) {
                    if (this.Tiles[x, y].IsWalkable) {
                        return this.Tiles[x, y];
                    }
                }
            }
            return null;
         }

         public string StringRepr() {
            // use x for blocked tiles and . for walkable tiles
            // also include agent positions with R for Red Team and B for Blue Team
            // string repr = "";
            // for (int y = this.Height - 1; y >= 0; y--) {
            //     for (int x = 0; x < this.Width; x++) {
            //         Vector2Int pos = new Vector2Int(x, y);
            //         Agent agent = GetAgentAtPosition(pos);
            //         if (agent != null) {
            //             repr += agent.Team == Team.Red ? "R" : "B";
            //         } else {
            //             repr += this.Tiles[x, y].IsWalkable ? "." : "X";
            //         }
            //     }
            //     repr += "\n";
            // }
            // return repr;
            return "";
        }
    }
}
