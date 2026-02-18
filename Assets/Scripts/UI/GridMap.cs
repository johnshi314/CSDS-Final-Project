using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;

namespace NetFlower.UI {

    public class GridMap : MonoBehaviour {

        [Header("Map Management")]
        public string mapName = "New World";
        private MapManager mapManager;
        
        [Header("Team Setup")]
        [SerializeField] private List<Agent> redAgents = new();
        [SerializeField] private List<Agent> blueAgents = new();
        [SerializeField] private List<Vector2Int> redSpawnPoints = new();
        [SerializeField] private List<Vector2Int> blueSpawnPoints = new();


        [Header("Map Definition")]
        [SerializeField] private Tilemap tilemap;
        [SerializeField] private Tilemap walkableTilemap;
        
        [Header("Tile Visualization")]
        [SerializeField] private bool enableTileVisuals = true;
        [SerializeField] private TileBase highlightTileAsset;
        [SerializeField] private Color tileHoverColor = Color.yellow;
        [SerializeField, Range(0f, 1f)] private float tileHoverAlpha = 0.5f;
        
        [Header("Debug Visualization")]
        [SerializeField] private bool showMapBoundsGizmos = true;
        [SerializeField] private Color walkableColor = Color.green;
        [SerializeField] private Color unwalkableColor = Color.red;

        // Internal state
        private TileVisualizer tileVisualizer;
        private Vector2Int tilemapBoundsMin; // Offset for tilemap coordinates (could be negative)
        private bool IsMapReady => mapManager != null && mapManager.HasActiveMap;
        private Map ActiveMap => IsMapReady ? mapManager.ActiveMap : null;

        // Cached references
        public MapManager MapManager => mapManager;
        public TileVisualizer TileVisualizer => tileVisualizer;
        public IReadOnlyList<Agent> RedAgents => IsMapReady ? mapManager.redTeam.Members : redAgents;
        public IReadOnlyList<Agent> BlueAgents => IsMapReady ? mapManager.blueTeam.Members : blueAgents;
        public IReadOnlyList<Vector2Int> RedSpawnPoints => IsMapReady ? mapManager.ActiveMap.RedSpawnPoints : redSpawnPoints;
        public IReadOnlyList<Vector2Int> BlueSpawnPoints => IsMapReady ? mapManager.ActiveMap.BlueSpawnPoints : blueSpawnPoints;
        private IEnumerable<Agent> ConfiguredAgents {
            get {
                foreach (Agent agent in redAgents) {
                    if (agent != null) yield return agent;
                }
                foreach (Agent agent in blueAgents) {
                    if (agent != null) yield return agent;
                }
            }
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            // This game object (Should be the Grid object in the scene)
            GameObject thisObject = this.gameObject;

            if (tilemap == null) {
                // Get the Tilemap component from the child objects
                tilemap = thisObject.GetComponentInChildren<Tilemap>();
            }

            // Find walkable tilemap if not assigned
            if (walkableTilemap == null) {
                Transform walkableChild = thisObject.transform.Find("Walkable");
                if (walkableChild != null) {
                    walkableTilemap = walkableChild.GetComponent<Tilemap>();
                }
            }

            if (walkableTilemap == null) {
                Debug.LogError("Walkable tilemap not found! Please assign a walkable tilemap or create a child GameObject named 'Walkable' with a Tilemap component.");
                return;
            }

            // Load walkability data from tilemap
            WalkabilityLoader.WalkabilityData walkabilityData = WalkabilityLoader.LoadFromTilemap(tilemap, walkableTilemap);
            
            if (walkabilityData == null || walkabilityData.Walkability == null) {
                Debug.LogError("Failed to load walkability data from tilemap");
                return;
            }

            // Store the tilemap bounds offset
            tilemapBoundsMin = walkabilityData.BoundsMin;
            Debug.Log($"Tilemap bounds start at: {tilemapBoundsMin}");

            // Hide the walkable tilemap after loading (editor-only purpose)
            TilemapRenderer walkableRenderer = walkableTilemap.GetComponent<TilemapRenderer>();
            if (walkableRenderer != null) {
                walkableRenderer.enabled = false;
            }

            Team redTeam = new Team("Red", TeamColor.Red, redAgents);
            Team blueTeam = new Team("Blue", TeamColor.Blue, blueAgents);

            // Build map manager and map data together
            mapManager = new MapManager(
                redTeam,
                blueTeam,
                mapName,
                walkabilityData.Walkability,
                redSpawnPoints,
                blueSpawnPoints
            );

            if (!IsMapReady) {
                Debug.LogError("GridMap: Failed to initialize MapManager with an active map.");
                return;
            }

            Map map = ActiveMap;

            Debug.Log("Map Name: " + map.MapName);
            Debug.Log("Map Width: " + map.Width);
            Debug.Log("Map Height: " + map.Height);

            // Set up tile visualizer if enabled
            if (enableTileVisuals) {
                SetupTileVisualizer();
            }
            
            // Visually position initial agents after MapManager has placed them
            PositionInitialAgents();
        }

        /// <summary>
        /// Visually positions all initial agents at their spawn points.
        /// Called after MapManager has placed agents logically.
        /// </summary>
        private void PositionInitialAgents() {
            if (!IsMapReady) return;

            foreach (Agent agent in ConfiguredAgents) {
                Tile currentTile = ActiveMap.GetCurrentTile(agent);
                if (currentTile != null) {
                    // Convert map index to world position and update agent's transform
                    Vector3 worldPos = MapIndexToWorldPosition(currentTile.Position, agent.transform.position.z);
                    agent.transform.position = worldPos;
                    Debug.Log($"GridMap: Positioned {agent.Name} at world position {worldPos} (map index {currentTile.Position})");
                } else {
                    Debug.LogWarning($"GridMap: Agent {agent.Name} not placed on map, skipping visual positioning.");
                }
            }
        }
        
        /// <summary>
        /// Sets up the tile visualizer for colliders and hover highlighting.
        /// </summary>
        private void SetupTileVisualizer() {
            tileVisualizer = GetComponent<TileVisualizer>();
            if (tileVisualizer == null) {
                tileVisualizer = gameObject.AddComponent<TileVisualizer>();
            }
            
            // Configure the visualizer
            tileVisualizer.Initialize(tilemap, highlightTileAsset, tileHoverColor, tileHoverAlpha);
        }

        // Update is called once per frame
        void Update()
        {
            // Sync inspector values to visualizer in real-time (allows runtime tweaking)
            if (tileVisualizer != null) {
                tileVisualizer.UpdateHoverSettings(tileHoverColor, tileHoverAlpha);
            }
        }

#region Tile Queries
        /// <summary>
        /// Gets the Map's Tile object currently under the cursor.
        /// Returns null if no tile is hovered.
        /// </summary>
        public Tile GetHoveredTile() {
            if (tileVisualizer == null || !IsMapReady) return null;
            
            Vector3Int hoveredCell = tileVisualizer.GetHoveredCell();
            
            // Check if a valid tile is hovered (not the sentinel value)
            if (hoveredCell.x == -999 && hoveredCell.y == -999) {
                return null;
            }
            
            // Convert from tilemap coordinates to map array indices
            Vector2Int tilemapCoord = new Vector2Int(hoveredCell.x, hoveredCell.y);
            Vector2Int mapIndex = TilemapToMapIndex(tilemapCoord);
            return ActiveMap.GetTileAtPosition(mapIndex);
        }

        /// <summary>
        /// Gets the Map's Tile object at a specific tilemap grid position.
        /// Returns null if the position is out of bounds.
        /// </summary>
        public Tile GetTileAt(Vector2Int tilemapPosition) {
            if (!IsMapReady) return null;
            Vector2Int mapIndex = TilemapToMapIndex(tilemapPosition);
            return ActiveMap.GetTileAtPosition(mapIndex);
        }

        /// <summary>
        /// Gets the Map's Tile object at a specific tilemap grid position.
        /// Returns null if the position is out of bounds.
        /// </summary>
        public Tile GetTileAt(int x, int y) {
            return GetTileAt(new Vector2Int(x, y));
        }
#endregion

#region Tile Highlighting

        /// <summary>
        /// Highlights all tiles within movement range of the specified agent.
        /// Uses Manhattan distance calculation and only includes walkable tiles.
        /// Returns the list of highlighted tile positions.
        /// </summary>
        public List<Vector3Int> HighlightMovementRange(Agent agent, Color highlightColor, float alpha = 0.6f) {
            if (!IsMapReady || tileVisualizer == null || agent == null) {
                Debug.LogWarning("GridMap: Cannot highlight movement range - map, visualizer, or agent is null");
                return new List<Vector3Int>();
            }

            // Verify agent is on this map
            Tile currentTile = ActiveMap.GetCurrentTile(agent);
            if (currentTile == null) {
                Debug.LogWarning($"GridMap: Agent {agent.Name} is not registered on this map");
                return new List<Vector3Int>();
            }

            // Get tiles within movement range
            List<Tile> movableTiles = ActiveMap.GetMovableTiles(agent);
            List<Vector3Int> highlightPositions = new List<Vector3Int>();

            // Convert tile positions from map indices to tilemap coordinates for highlighting
            foreach (Tile tile in movableTiles) {
                // Tile.Position is in map array indices, convert to tilemap coordinates
                Vector2Int tilemapCoord = MapIndexToTilemap(tile.Position);
                Vector3Int cellPos = new Vector3Int(tilemapCoord.x, tilemapCoord.y, 0);
                highlightPositions.Add(cellPos);
            }

            // Highlight them
            tileVisualizer.HighlightCells(highlightPositions, highlightColor, alpha);

            // Return the list of highlighted positions for reference
            return highlightPositions;
        }

        /// <summary>
        /// Clears all persistent tile highlights (keeps hover highlighting active).
        /// </summary>
        public void ClearHighlights() {
            if (tileVisualizer != null) {
                tileVisualizer.ClearAllHighlights();
            }
        }
#endregion

#region Coordinate Conversions
        /// <summary>
        /// Converts tilemap coordinates to Map array indices.
        /// </summary>
        public Vector2Int TilemapToMapIndex(Vector2Int tilemapCoord) {
            return new Vector2Int(tilemapCoord.x - tilemapBoundsMin.x, tilemapCoord.y - tilemapBoundsMin.y);
        }

        /// <summary>
        /// Converts Map array indices to tilemap coordinates.
        /// </summary>
        public Vector2Int MapIndexToTilemap(Vector2Int mapIndex) {
            return new Vector2Int(mapIndex.x + tilemapBoundsMin.x, mapIndex.y + tilemapBoundsMin.y);
        }

        /// <summary>
        /// Converts a logical grid position to Unity world position.
        /// Uses the tilemap's cell center for accurate positioning.
        /// </summary>
        public Vector3 TilemapToWorldPosition(Vector2Int tilemapPosition) {
            if (tilemap == null) {
                Debug.LogWarning("GridMap: Tilemap is null, returning direct conversion");
                return new Vector3(tilemapPosition.x, tilemapPosition.y, 0);
            }
            
            Vector3Int cellPosition = new Vector3Int(tilemapPosition.x, tilemapPosition.y, 0);
            return tilemap.GetCellCenterWorld(cellPosition);
        }

        /// <summary>
        /// Converts a logical grid position to Unity world position, preserving Z coordinate.
        /// </summary>
        public Vector3 TilemapToWorldPosition(Vector2Int tilemapPosition, float z) {
            Vector3 worldPos = TilemapToWorldPosition(tilemapPosition);
            worldPos.z = z;
            return worldPos;
        }

        /// <summary>
        /// Converts a map array index to Unity world position.
        /// </summary>
        public Vector3 MapIndexToWorldPosition(Vector2Int mapIndex) {
            Vector2Int tilemapCoord = MapIndexToTilemap(mapIndex);
            return TilemapToWorldPosition(tilemapCoord);
        }

        /// <summary>
        /// Converts a map array index to Unity world position, preserving Z coordinate.
        /// </summary>
        public Vector3 MapIndexToWorldPosition(Vector2Int mapIndex, float z) {
            Vector3 worldPos = MapIndexToWorldPosition(mapIndex);
            worldPos.z = z;
            return worldPos;
        }
#endregion

#region Agent Position Queries
        /// <summary>
        /// Registers an agent on the map at a specific tilemap grid position.
        /// Also sets the agent's GameObject position to match the grid position.
        /// </summary>
        public bool TryRegisterAgent(Agent agent, Vector2Int tilemapPosition) {
            return TryRegisterAgentByMapIndex(agent, TilemapToMapIndex(tilemapPosition));
        }

        /// <summary>
        /// Registers an agent on the map using map array indices.
        /// Also sets the agent's GameObject position to match the grid position.
        /// </summary>
        public bool TryRegisterAgentByMapIndex(Agent agent, Vector2Int mapIndex) {
            if (!IsMapReady || agent == null) {
                Debug.LogWarning("GridMap: Cannot register agent - map manager, map, or agent is null");
                return false;
            }

            // Register with the map manager
            if (!mapManager.PlaceAgent(agent, mapIndex)) {
                Debug.LogWarning($"GridMap: Could not place {agent.Name} at map index {mapIndex}");
                return false;
            }

            // Update visual position
            Vector3 worldPos = MapIndexToWorldPosition(mapIndex, agent.transform.position.z);
            agent.transform.position = worldPos;
            Vector2Int tilemapCoord = MapIndexToTilemap(mapIndex);
            Debug.Log($"Registered {agent.Name} at map index {mapIndex} (tilemap coord {tilemapCoord}, world pos {worldPos})");
            return true;
        }

        /// <summary>
        /// Moves an agent to a new tilemap grid position.
        /// Updates both the map data and the GameObject's visual position.
        /// </summary>
        public bool TryMoveAgent(Agent agent, Vector2Int newTilemapPosition) {
            return TryMoveAgentByMapIndex(agent, TilemapToMapIndex(newTilemapPosition));
        }

        /// <summary>
        /// Moves an agent to a new position using map array indices.
        /// Updates both the map data and the GameObject's visual position.
        /// </summary>
        public bool TryMoveAgentByMapIndex(Agent agent, Vector2Int newMapIndex) {
            // Is this GridMap initialized properly?
            if (!IsMapReady || agent == null) {
                Debug.LogWarning("GridMap: Cannot move agent - map manager, map, or agent is null");
                return false;
            }

            // Update map data via map manager
            if (!mapManager.RequestMove(agent, newMapIndex)) {
                Debug.LogWarning($"GridMap: Could not move {agent.Name} to map index {newMapIndex}");
                return false;
            }

            // Update visual position (keep original Z)
            Vector3 worldPos = MapIndexToWorldPosition(newMapIndex, agent.transform.position.z);
            MoveAgentVisually(agent, worldPos);

            // Optional debug log to see coordinate conversions in action
            DebugPrintMapCoordinate(newMapIndex);
            return true;
        }

        /// <summary>
        /// Gets the current tilemap grid position of an agent.
        /// </summary>
        public Vector2Int? GetAgentGridPosition(Agent agent) {
            if (!IsMapReady || agent == null) return null;
            
            Tile currentTile = ActiveMap.GetCurrentTile(agent);
            if (currentTile == null) return null;
            
            // Convert map index back to tilemap coordinates
            return MapIndexToTilemap(currentTile.Position);
        }

        /// <summary>
        /// Gets the current map array index of an agent.
        /// </summary>
        public Vector2Int? GetAgentMapIndex(Agent agent) {
            if (!IsMapReady || agent == null) return null;
            
            Tile currentTile = ActiveMap.GetCurrentTile(agent);
            return currentTile?.Position;
        }
#endregion

#region Map Dimension Queries
        /// <summary>
        /// Gets the dimensions of the active map in map-array indices.
        /// Returns (0,0) if no active map exists.
        /// </summary>
        public Vector2Int GetMapDimensions() {
            if (!IsMapReady) return Vector2Int.zero;
            return ActiveMap.GetDimensions();
        }

        /// <summary>
        /// Returns true if the given map-array index is inside the active map bounds.
        /// </summary>
        public bool InBounds(Vector2Int mapIndex) {
            return IsMapReady && ActiveMap.InBounds(mapIndex);
        }

        /// <summary>
        /// Returns true if the tile at map-array index is walkable.
        /// </summary>
        public bool IsWalkable(Vector2Int mapIndex) {
            return IsMapReady && ActiveMap.IsWalkable(mapIndex);
        }

        /// <summary>
        /// Finds the first walkable tile in row-major order.
        /// </summary>
        public bool TryGetFirstWalkableTile(out Tile tile) {
            tile = null;
            if (!IsMapReady) return false;

            tile = ActiveMap.TryGetFirstWalkableTile();
            return tile != null;
         }
#endregion

#region Private Methods
        /// <summary>
        /// Updates an agent GameObject's visual world position.
        /// </summary>
        private void MoveAgentVisually(Agent agent, Vector3 worldPosition) {
            if (agent == null) return;
            agent.transform.position = worldPosition;
        }

        private void DebugPrintMapCoordinate(Vector2Int mapCoord) {
            Debug.Log($"Map: {mapCoord} | Tilemap: {MapIndexToTilemap(mapCoord)} | World: {MapIndexToWorldPosition(mapCoord)}");
        }
#endregion

        /// <summary>
        /// Draws gizmos showing the bounds of each tile in the map.
        /// Green for walkable tiles, red for unwalkable tiles.
        /// </summary>
        void OnDrawGizmos() {
            if (!showMapBoundsGizmos || !IsMapReady || tilemap == null) return;
            // Draw each tile in the map
            for (int y = 0; y < ActiveMap.Height; y++) {
                for (int x = 0; x < ActiveMap.Width; x++) {
                    Vector2Int mapIndex = new Vector2Int(x, y);
                    Vector2Int tilemapCoord = MapIndexToTilemap(mapIndex);
                    
                    // Get the world position and size of this tile
                    Vector3Int cellPosition = new Vector3Int(tilemapCoord.x, tilemapCoord.y, 0);
                    Vector3 worldPos = tilemap.GetCellCenterWorld(cellPosition);
                    
                    // Set color based on walkability
                    Gizmos.color = ActiveMap.IsWalkable(mapIndex) ? walkableColor : unwalkableColor;
                    
                    // Draw wire cube to outline the tile
                    Gizmos.DrawWireSphere(worldPos, 0.1f); // Using a sphere for better visibility, adjust size as needed
                }
            }
        }
    }
}
