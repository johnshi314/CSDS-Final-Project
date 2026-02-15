using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Tilemaps;
using NetFlower;

namespace NetFlower.UI {

public class GridUI : MonoBehaviour {

    public Map map;
    public  Tilemap tilemap;
    
    [Header("Walkability Configuration")]
    [SerializeField] private Tilemap walkableTilemap;
    
    [Header("Tile Visualization")]
    [SerializeField] private bool enableTileVisuals = true;
    [SerializeField] private TileBase highlightTileAsset;
    [SerializeField] private Color tileHoverColor = Color.yellow;
    [SerializeField, Range(0f, 1f)] private float tileHoverAlpha = 0.5f;
    
    private TileVisualizer tileVisualizer;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // this game object (Should be the Grid object in the scene)
        GameObject thisObject = this.gameObject;

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
        bool[,] tiles = WalkabilityLoader.LoadFromTilemap(tilemap, walkableTilemap);
        
        if (tiles == null) {
            Debug.LogError("Failed to load walkability data from tilemap");
            return;
        }

        // Hide the walkable tilemap after loading (editor-only purpose)
        TilemapRenderer walkableRenderer = walkableTilemap.GetComponent<TilemapRenderer>();
        if (walkableRenderer != null) {
            walkableRenderer.enabled = false;
        }

        Vector2Int[] spawnPoints = new Vector2Int[] {};

        map.Initialize(
            "World 1",
            tiles,
            spawnPoints
        );

        Debug.Log("Map Name: " + map.MapName);
        Debug.Log("Map Width: " + map.Width);
        Debug.Log("Map Height: " + map.Height);

        // Set up tile visualizer if enabled
        if (enableTileVisuals) {
            SetupTileVisualizer();
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
        
        // Convert to Vector2Int and get the Map's Tile object
        Vector2Int tilePos = new Vector2Int(hoveredCell.x, hoveredCell.y);
        return map.GetTileAtPosition(tilePos);
    }

    /// <summary>
    /// Gets the Map's Tile object at a specific grid position.
    /// Returns null if the position is out of bounds.
    /// </summary>
    public Tile GetTileAt(Vector2Int position) {
        if (map == null) return null;
        return map.GetTileAtPosition(position);
    }

    /// <summary>
    /// Gets the Map's Tile object at a specific grid position.
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
            Debug.LogWarning("GridUI: Cannot highlight movement range - map, visualizer, or agent is null");
            return new List<Vector3Int>();
        }

        // Verify agent is on this map
        Tile currentTile = map.GetCurrentTile(agent);
        if (currentTile == null) {
            Debug.LogWarning($"GridUI: Agent {agent.Name} is not registered on this map");
            return new List<Vector3Int>();
        }

        // Get tiles within movement range
        List<Vector3Int> movableTiles = GetMovableTiles(agent);

        // Highlight them
        tileVisualizer.HighlightCells(movableTiles, highlightColor, alpha);

        return movableTiles;
    }

    /// <summary>
    /// Calculates and returns all tile positions the agent can move to.
    /// Uses Algorithm-based distance calculation based on agent's movement range and walkability of tiles.
    /// </summary>
    public List<Vector3Int> GetMovableTiles(Agent agent) {
        List<Vector3Int> movableTiles = new List<Vector3Int>();
        // TODO: Implement pathfinding-based movement range calculation that accounts for walkability and obstacles.

        return movableTiles;
    }

    /// <summary>
    /// Clears all persistent tile highlights (keeps hover highlighting active).
    /// </summary>
    public void ClearHighlights() {
        if (tileVisualizer != null) {
            tileVisualizer.ClearAllHighlights();
        }
    }
}

}
