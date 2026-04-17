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

        // Effect instances on tiles (tick and expire via TickEffects)
        private Dictionary<Vector2Int, List<AbilityEffectInstance>> tileEffects = new Dictionary<Vector2Int, List<AbilityEffectInstance>>();

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
            tileEffects.Clear();
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

        /// <summary>
        /// Removes an agent from the map occupancy graph (e.g. before applying server-authoritative spawn layout).
        /// </summary>
        public void UnregisterAgent(Agent agent) {
            if (agent == null) return;
            if (currentTiles.TryGetValue(agent, out Tile tile)) {
                agentsByPosition.Remove(tile.Position);
                currentTiles.Remove(agent);
            }
            startTiles.Remove(agent);
            paths.Remove(agent);
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
                return null;
            }
            return agentsByPosition.TryGetValue(tilePos, out Agent agent) ? agent : null;
        }

        public Tile GetTileAtPosition(Vector2Int tilePos) {
            int x = tilePos.x;
            int y = tilePos.y;
            if (x < 0 || x >= Width || y < 0 || y >= Height) {
                return null;
            }
            return this.Tiles[x, y];
        }

        /// <summary>
        /// Add a duration effect on a tile (called after ability resolve when effect has duration conditions).
        /// </summary>
        public void AddEffect(Tile tile, AbilityEffectInstance instance) {
            if (tile == null || instance == null) return;
            if (!tileEffects.TryGetValue(tile.Position, out var list)) {
                list = new List<AbilityEffectInstance>();
                tileEffects[tile.Position] = list;
            }
            list.Add(instance);
        }

        /// <summary>
        /// Get all effect instances on a tile (for display or query).
        /// </summary>
        public IReadOnlyList<AbilityEffectInstance> GetEffectsOnTile(Tile tile) {
            if (tile == null || !tileEffects.TryGetValue(tile.Position, out var list)) return new List<AbilityEffectInstance>();
            return list;
        }

        /// <summary>
        /// Called each turn (e.g. from TurnManager): remove tile-bound effects expired at the given turn number.
        /// </summary>
        /// <param name="currentTurn">Current turn number (used for expiry: effect expires when currentTurn >= TurnApplied + duration).</param>
        public void TickEffects(int currentTurn) {
            var toRemove = new List<(Vector2Int pos, AbilityEffectInstance inst)>();
            foreach (var kv in tileEffects) {
                foreach (var inst in kv.Value) {
                    if (inst.IsExpired(currentTurn)) toRemove.Add((kv.Key, inst));
                }
            }
            foreach (var (pos, inst) in toRemove) {
                if (tileEffects.TryGetValue(pos, out var list)) {
                    list.Remove(inst);
                    if (list.Count == 0) tileEffects.Remove(pos);
                }
            }
        }

        /// <summary>
        /// All agents currently on the map (for ticking agent-bound effects each turn).
        /// </summary>
        public IEnumerable<Agent> GetRegisteredAgents() => currentTiles.Keys;

        /// <summary>
        /// BFS flood fill from the agent's current tile, bounded by movement range.
        /// Only traverses walkable tiles not occupied by other agents.
        /// </summary>
        public List<Tile> GetMovableTiles(Agent agent) {
            List<Tile> movableTiles = new List<Tile>();
            Tile startTile = GetCurrentTile(agent);
            if (startTile == null) return movableTiles;

            int range = (int)agent.MovementRange;
            var queue = new Queue<(Tile tile, int dist)>();
            var visited = new HashSet<Vector2Int>();

            queue.Enqueue((startTile, 0));
            visited.Add(startTile.Position);

            Vector2Int[] directions = {
                Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
            };

            while (queue.Count > 0) {
                var (current, dist) = queue.Dequeue();

                if (dist > 0) {
                    movableTiles.Add(current);
                }

                if (dist >= range) continue;

                foreach (var dir in directions) {
                    Vector2Int neighborPos = current.Position + dir;
                    if (visited.Contains(neighborPos)) continue;
                    if (!InBounds(neighborPos)) continue;

                    Tile neighbor = Tiles[neighborPos.x, neighborPos.y];
                    if (!neighbor.IsWalkable) continue;

                    Agent occupant = GetAgentAtPosition(neighborPos);
                    if (occupant != null && occupant != agent) continue;

                    visited.Add(neighborPos);
                    queue.Enqueue((neighbor, dist + 1));
                }
            }

            return movableTiles;
        }

        /// <summary>
        /// Returns the shortest path (as a list of tiles) from start to end, or empty if unreachable.
        /// Only traverses walkable, non-diagonal tiles, and avoids other agents.
        /// </summary>
        public List<Tile> FindShortestPath(Vector2Int start, Vector2Int end) {
            var path = new List<Tile>();
            if (!InBounds(start) || !InBounds(end)) return path;
            if (start == end) {
                path.Add(GetTileAtPosition(start));
                return path;
            }
            var queue = new Queue<(Vector2Int pos, List<Tile> pathSoFar)>();
            var visited = new HashSet<Vector2Int>();
            queue.Enqueue((start, new List<Tile> { GetTileAtPosition(start) }));
            visited.Add(start);
            Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            while (queue.Count > 0) {
                var (current, currentPath) = queue.Dequeue();
                foreach (var dir in directions) {
                    Vector2Int neighbor = current + dir;
                    if (!InBounds(neighbor) || visited.Contains(neighbor)) continue;
                    Tile neighborTile = GetTileAtPosition(neighbor);
                    if (!neighborTile.IsWalkable) continue;
                    Agent occupant = GetAgentAtPosition(neighbor);
                    if (occupant != null) continue;
                    var newPath = new List<Tile>(currentPath) { neighborTile };
                    if (neighbor == end) {
                        return newPath;
                    }
                    queue.Enqueue((neighbor, newPath));
                    visited.Add(neighbor);
                }
            }
            return new List<Tile>(); // No path found
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
