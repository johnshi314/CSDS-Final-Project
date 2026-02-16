using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;
using NetFlower;

namespace NetFlower.UI {

    public class GridMap : MonoBehaviour {

        [Header("Map Management")]
        [SerializeField] private MapManager mapManager;
        
        [Header("Initial Agents")]
        [SerializeField] private List<Agent> initialAgents = new List<Agent>();
        
        [Header("Spawn Points")]
        [SerializeField] private Vector2Int[] spawnPoints = new Vector2Int[0];

        private Map map;
        public Map Map => map;
        public Tilemap tilemap;
        
        [Header("Walkability Configuration")]
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
        
        private TileVisualizer tileVisualizer;
        private Vector2Int tilemapBoundsMin; // Offset for tilemap coordinates (could be negative)

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            // This game object (Should be the Grid object in the scene)
            GameObject thisObject = this.gameObject;

            if (mapManager == null) {
                mapManager = FindFirstObjectByType<MapManager>();
            }
            if (mapManager == null) {
                Debug.LogError("GridMap: MapManager not found! Assign one in the inspector or add it to the scene.");
                return;
            }

            if (mapManager.ActiveMap != null) {
                map = mapManager.ActiveMap;
            }
            if (map == null) {
                // Get the Map component from this game object
                map = thisObject.GetComponent<Map>();
            }
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

            // Initialize the Map with the loaded walkability data and spawn points
            map.Initialize(
                "World 1",
                walkabilityData.Walkability,
                spawnPoints
            );

            // Pass initial agents to MapManager and initialize
            mapManager.SetInitialAgents(initialAgents);
            mapManager.Initialize(map);

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
            foreach (Agent agent in initialAgents) {
                if (agent == null) continue;
                
                Tile currentTile = map.GetCurrentTile(agent);
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

        /// <summary>
        /// Gets the TileVisualizer component for advanced highlighting operations.
        /// </summary>
        public TileVisualizer GetTileVisualizer() {
            return tileVisualizer;
        }

        /// <summary>
        /// Gets the Map's Tile object currently under the cursor.
        /// Returns null if no tile is hovered.
        /// </summary>
        public Tile GetHoveredTile() {
            if (tileVisualizer == null || map == null) return null;
            
            Vector3Int hoveredCell = tileVisualizer.GetHoveredCell();
            
            // Check if a valid tile is hovered (not the sentinel value)
            if (hoveredCell.x == -999 && hoveredCell.y == -999) {
                return null;
            }
            
            // Convert from tilemap coordinates to map array indices
            Vector2Int tilemapCoord = new Vector2Int(hoveredCell.x, hoveredCell.y);
            Vector2Int mapIndex = TilemapToMapIndex(tilemapCoord);
            return map.GetTileAtPosition(mapIndex);
        }

        /// <summary>
        /// Gets the Map's Tile object at a specific tilemap grid position.
        /// Returns null if the position is out of bounds.
        /// </summary>
        public Tile GetTileAt(Vector2Int tilemapPosition) {
            if (map == null) return null;
            Vector2Int mapIndex = TilemapToMapIndex(tilemapPosition);
            return map.GetTileAtPosition(mapIndex);
        }

        /// <summary>
        /// Gets the Map's Tile object at a specific tilemap grid position.
        /// Returns null if the position is out of bounds.
        /// </summary>
        public Tile GetTileAt(int x, int y) {
            return GetTileAt(new Vector2Int(x, y));
        }

        /// <summary>
        /// Highlights all tiles within movement range of the specified agent.
        /// Uses Manhattan distance calculation and only includes walkable tiles.
        /// Returns the list of highlighted tile positions.
        /// </summary>
        public List<Vector3Int> HighlightMovementRange(Agent agent, Color highlightColor, float alpha = 0.6f) {
            if (map == null || tileVisualizer == null || agent == null) {
                Debug.LogWarning("GridMap: Cannot highlight movement range - map, visualizer, or agent is null");
                return new List<Vector3Int>();
            }

            // Verify agent is on this map
            Tile currentTile = map.GetCurrentTile(agent);
            if (currentTile == null) {
                Debug.LogWarning($"GridMap: Agent {agent.Name} is not registered on this map");
                return new List<Vector3Int>();
            }

            // Get tiles within movement range
            List<Tile> movableTiles = map.GetMovableTiles(agent);
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
        /// Clears all persistent tile highlights (keeps hover highlighting active).
        /// </summary>
        public void ClearHighlights() {
            if (tileVisualizer != null) {
                tileVisualizer.ClearAllHighlights();
            }
        }

        /// <summary>
        /// Converts a logical grid position to Unity world position.
        /// Uses the tilemap's cell center for accurate positioning.
        /// </summary>
        public Vector3 GridToWorldPosition(Vector2Int tilemapPosition) {
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
        public Vector3 GridToWorldPosition(Vector2Int tilemapPosition, float z) {
            Vector3 worldPos = GridToWorldPosition(tilemapPosition);
            worldPos.z = z;
            return worldPos;
        }

        /// <summary>
        /// Converts a map array index to Unity world position.
        /// </summary>
        public Vector3 MapIndexToWorldPosition(Vector2Int mapIndex) {
            Vector2Int tilemapCoord = MapIndexToTilemap(mapIndex);
            return GridToWorldPosition(tilemapCoord);
        }

        /// <summary>
        /// Converts a map array index to Unity world position, preserving Z coordinate.
        /// </summary>
        public Vector3 MapIndexToWorldPosition(Vector2Int mapIndex, float z) {
            Vector3 worldPos = MapIndexToWorldPosition(mapIndex);
            worldPos.z = z;
            return worldPos;
        }

        /// <summary>
        /// Registers an agent on the map at a specific tilemap grid position.
        /// Also sets the agent's GameObject position to match the grid position.
        /// </summary>
        public void RegisterAgent(Agent agent, Vector2Int tilemapPosition) {
            if (mapManager == null || mapManager.ActiveMap == null || agent == null) {
                Debug.LogWarning("GridMap: Cannot register agent - map manager, map, or agent is null");
                return;
            }

            // Convert tilemap coordinates to map array indices
            Vector2Int mapIndex = TilemapToMapIndex(tilemapPosition);

            // Register with the map
            if (!mapManager.PlaceAgent(agent, mapIndex)) {
                Debug.LogWarning($"GridMap: Could not place {agent.Name} at map index {mapIndex}");
                return;
            }

            // Update visual position using tilemap conversion
            agent.transform.position = GridToWorldPosition(tilemapPosition, agent.transform.position.z);
            Debug.Log($"Registered {agent.Name} at tilemap position {tilemapPosition} (map index {mapIndex})");
        }

        /// <summary>
        /// Registers an agent on the map using map array indices.
        /// Also sets the agent's GameObject position to match the grid position.
        /// </summary>
        public void RegisterAgentByMapIndex(Agent agent, Vector2Int mapIndex) {
            if (mapManager == null || mapManager.ActiveMap == null || agent == null) {
                Debug.LogWarning("GridMap: Cannot register agent - map manager, map, or agent is null");
                return;
            }

            // Register with the map manager
            if (!mapManager.PlaceAgent(agent, mapIndex)) {
                Debug.LogWarning($"GridMap: Could not place {agent.Name} at map index {mapIndex}");
                return;
            }

            // Update visual position
            Vector3 worldPos = MapIndexToWorldPosition(mapIndex, agent.transform.position.z);
            agent.transform.position = worldPos;
            Vector2Int tilemapCoord = MapIndexToTilemap(mapIndex);
            Debug.Log($"Registered {agent.Name} at map index {mapIndex} (tilemap coord {tilemapCoord}, world pos {worldPos})");
        }

        /// <summary>
        /// Moves an agent to a new tilemap grid position.
        /// Updates both the map data and the GameObject's visual position.
        /// </summary>
        public void MoveAgent(Agent agent, Vector2Int newTilemapPosition) {
            if (mapManager == null || mapManager.ActiveMap == null || agent == null) {
                Debug.LogWarning("GridMap: Cannot move agent - map manager, map, or agent is null");
                return;
            }

            // Verify agent is registered
            Tile currentTile = mapManager.ActiveMap.GetCurrentTile(agent);
            if (currentTile == null) {
                Debug.LogWarning($"GridMap: Agent {agent.Name} is not registered on this map");
                return;
            }

            // Convert tilemap coordinates to map array indices
            Vector2Int mapIndex = TilemapToMapIndex(newTilemapPosition);

            // Update map data
            if (!mapManager.RequestMove(agent, mapIndex)) {
                Debug.LogWarning($"GridMap: Could not move {agent.Name} to map index {mapIndex}");
                return;
            }

            // Update visual position using tilemap conversion (keep original Z)
            agent.transform.position = GridToWorldPosition(newTilemapPosition, agent.transform.position.z);
            Debug.Log($"Moved {agent.Name} to tilemap position {newTilemapPosition} (map index {mapIndex})");
        }

        /// <summary>
        /// Moves an agent to a new position using map array indices.
        /// Updates both the map data and the GameObject's visual position.
        /// </summary>
        public void MoveAgentByMapIndex(Agent agent, Vector2Int newMapIndex) {
            if (mapManager == null || mapManager.ActiveMap == null || agent == null) {
                Debug.LogWarning("GridMap: Cannot move agent - map manager, map, or agent is null");
                return;
            }

            // Verify agent is registered
            Tile currentTile = mapManager.ActiveMap.GetCurrentTile(agent);
            if (currentTile == null) {
                Debug.LogWarning($"GridMap: Agent {agent.Name} is not registered on this map");
                return;
            }

            // Update map data via map manager
            if (!mapManager.RequestMove(agent, newMapIndex)) {
                Debug.LogWarning($"GridMap: Could not move {agent.Name} to map index {newMapIndex}");
                return;
            }

            // Update visual position (keep original Z)
            Vector3 worldPos = MapIndexToWorldPosition(newMapIndex, agent.transform.position.z);
            agent.transform.position = worldPos;
            Vector2Int tilemapCoord = MapIndexToTilemap(newMapIndex);
            Debug.Log($"Moved {agent.Name} to map index {newMapIndex} (tilemap coord {tilemapCoord}, world pos {worldPos})");
        }

        /// <summary>
        /// Gets the current tilemap grid position of an agent.
        /// </summary>
        public Vector2Int? GetAgentGridPosition(Agent agent) {
            if (map == null || agent == null) return null;
            
            Tile currentTile = map.GetCurrentTile(agent);
            if (currentTile == null) return null;
            
            // Convert map index back to tilemap coordinates
            return MapIndexToTilemap(currentTile.Position);
        }

        /// <summary>
        /// Gets the current map array index of an agent.
        /// </summary>
        public Vector2Int? GetAgentMapIndex(Agent agent) {
            if (map == null || agent == null) return null;
            
            Tile currentTile = map.GetCurrentTile(agent);
            return currentTile?.Position;
        }

        /// <summary>
        /// Draws gizmos showing the bounds of each tile in the map.
        /// Green for walkable tiles, red for unwalkable tiles.
        /// </summary>
        void OnDrawGizmos() {
            if (!showMapBoundsGizmos || map == null || tilemap == null) return;

            // Draw each tile in the map
            for (int y = 0; y < map.Height; y++) {
                for (int x = 0; x < map.Width; x++) {
                    Tile tile = map.Tiles[x, y];
                    Vector2Int mapIndex = new Vector2Int(x, y);
                    Vector2Int tilemapCoord = MapIndexToTilemap(mapIndex);
                    
                    // Get the world position and size of this tile
                    Vector3Int cellPosition = new Vector3Int(tilemapCoord.x, tilemapCoord.y, 0);
                    Vector3 worldPos = tilemap.GetCellCenterWorld(cellPosition);
                    Vector3 cellSize = tilemap.cellSize;
                    
                    // Set color based on walkability
                    Gizmos.color = tile.IsWalkable ? walkableColor : unwalkableColor;
                    
                    // Draw wire cube to outline the tile
                    Gizmos.DrawWireCube(worldPos, new Vector3(cellSize.x, cellSize.y, 0.1f));
                }
            }
        }
    }

}
