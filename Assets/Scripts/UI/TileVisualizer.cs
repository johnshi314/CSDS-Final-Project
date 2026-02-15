using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace NetFlower.UI {

/// <summary>
/// Handles visual representation of tilemap tiles with hover highlighting using a duplicate tilemap.
/// Supports highlighting individual or multiple tiles for game mechanics (hover, movement range, etc).
/// </summary>
public class TileVisualizer : MonoBehaviour {
    // Configuration (set via Initialize)
    private Tilemap baseTilemap;
    private TileBase highlightTileAsset;
    private Color hoverHighlightColor = Color.yellow;
    private float hoverHighlightAlpha = 0.5f;
    
    // Runtime state
    private Tilemap highlightTilemap;
    private GameObject highlightTilemapObj;
    private TilemapCollider2D tilemapCollider;
    private Vector3Int lastHoveredPos = new Vector3Int(-999, -999, 0);
    private HashSet<Vector3Int> persistentHighlights = new HashSet<Vector3Int>();
    private bool hoverEnabled = true;
    private bool isInitialized = false;

    /// <summary>
    /// Initialize the TileVisualizer with configuration from GridUI.
    /// Call this instead of using inspector fields.
    /// </summary>
    public void Initialize(Tilemap tilemap, TileBase highlightTile, Color hoverColor, float hoverAlpha = 0.5f) {
        this.baseTilemap = tilemap;
        this.highlightTileAsset = highlightTile;
        this.hoverHighlightColor = hoverColor;
        this.hoverHighlightAlpha = hoverAlpha;

        if (baseTilemap == null) {
            Debug.LogError("TileVisualizer: Cannot initialize with null tilemap!");
            return;
        }

        CreateHighlightTilemap();
        SetupTilemapCollider();
        isInitialized = true;
    }

    void Update() {
        if (isInitialized && hoverEnabled) {
            HandleMouseHover();
        }
    }

    /// <summary>
    /// Updates hover color and alpha settings in real-time.
    /// Automatically refreshes the current hovered tile if it changed.
    /// </summary>
    public void UpdateHoverSettings(Color color, float alpha) {
        if (!isInitialized) return;
        
        bool changed = (hoverHighlightColor != color || hoverHighlightAlpha != alpha);
        hoverHighlightColor = color;
        hoverHighlightAlpha = alpha;
        
        // Refresh current hover tile if settings changed
        if (changed && lastHoveredPos.x != -999 && !persistentHighlights.Contains(lastHoveredPos)) {
            SetHighlightAtCell(lastHoveredPos, hoverHighlightColor, hoverHighlightAlpha);
        }
    }

    /// <summary>
    /// Creates a duplicate tilemap for highlighting that overlays the original.
    /// </summary>
    private void CreateHighlightTilemap() {
        // Create a new GameObject for the highlight tilemap
        highlightTilemapObj = new GameObject("HighlightTilemap");
        highlightTilemapObj.transform.SetParent(baseTilemap.transform.parent);
        highlightTilemapObj.transform.localPosition = Vector3.zero;

        // Get the Grid from the original tilemap
        Grid grid = baseTilemap.GetComponentInParent<Grid>();
        if (grid == null) {
            Debug.LogError("TileVisualizer: Grid not found!");
            return;
        }

        // Add the highlight tilemap to the same grid
        highlightTilemap = highlightTilemapObj.AddComponent<Tilemap>();
        TilemapRenderer tilemapRenderer = highlightTilemapObj.AddComponent<TilemapRenderer>();

        // Configure renderer for color tinting
        TilemapRenderer baseTilemapRenderer = baseTilemap.GetComponent<TilemapRenderer>();
        tilemapRenderer.sortingOrder = baseTilemapRenderer.sortingOrder + 1;
        tilemapRenderer.mode = TilemapRenderer.Mode.Individual; // Enable per-tile colors
        
        // Copy material from base tilemap (supports transparency out of the box)
        if (baseTilemapRenderer != null && baseTilemapRenderer.material != null) {
            tilemapRenderer.material = new Material(baseTilemapRenderer.material);
        }

        if (highlightTileAsset == null) {
            Debug.LogWarning("TileVisualizer: No highlight tile asset assigned!");
        }

        Debug.Log("TileVisualizer: Created highlight tilemap");
    }

    /// <summary>
    /// Ensures the tilemap has a TilemapCollider2D for accurate hover detection.
    /// </summary>
    private void SetupTilemapCollider() {
        tilemapCollider = baseTilemap.GetComponent<TilemapCollider2D>();
        if (tilemapCollider == null) {
            tilemapCollider = baseTilemap.gameObject.AddComponent<TilemapCollider2D>();
        }
    }

    /// <summary>
    /// Handles mouse hover detection using raycasting.
    /// </summary>
    private void HandleMouseHover() {
        if (Mouse.current == null || highlightTilemap == null || Camera.main == null) return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 mouseWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(
            mousePos.x,
            mousePos.y,
            -Camera.main.transform.position.z
        ));

        Vector3Int hoveredCellPos = new Vector3Int(-999, -999, 0);

        if (tilemapCollider != null) {
            Collider2D[] hits = Physics2D.OverlapPointAll(mouseWorldPos);
            for (int i = 0; i < hits.Length; i++) {
                if (hits[i] == tilemapCollider) {
                    Vector3Int cellPos = baseTilemap.WorldToCell(mouseWorldPos);
                    if (baseTilemap.HasTile(cellPos)) {
                        hoveredCellPos = cellPos;
                    }
                    break;
                }
            }
        } else {
            Vector3Int cellPos = baseTilemap.WorldToCell(mouseWorldPos);
            if (baseTilemap.HasTile(cellPos)) {
                hoveredCellPos = cellPos;
            }
        }

        // Only update if the hovered tile changed
        if (hoveredCellPos != lastHoveredPos) {
            // Clear previous hover highlight (but keep persistent highlights)
            if (lastHoveredPos.x != -999 && !persistentHighlights.Contains(lastHoveredPos)) {
                highlightTilemap.SetTile(lastHoveredPos, null);
                highlightTilemap.SetColor(lastHoveredPos, Color.white);
            }

            // Set new hover highlight
            if (hoveredCellPos.x != -999 && highlightTileAsset != null) {
                SetHighlightAtCell(hoveredCellPos, hoverHighlightColor, hoverHighlightAlpha);
                Debug.Log($"Entered tile: {hoveredCellPos}");
            }

            lastHoveredPos = hoveredCellPos;
        }
    }

    /// <summary>
    /// Internal helper to set a highlight tile at a specific cell.
    /// </summary>
    private void SetHighlightAtCell(Vector3Int cellPos, Color color, float alpha) {
        UnityEngine.Tilemaps.Tile sourceTile = highlightTileAsset as UnityEngine.Tilemaps.Tile;
        if (sourceTile != null && sourceTile.sprite != null) {
            UnityEngine.Tilemaps.Tile coloredTile = ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();
            coloredTile.sprite = sourceTile.sprite;
            coloredTile.colliderType = sourceTile.colliderType;
            coloredTile.flags = TileFlags.None; // Allow color changes
            
            Color highlightColor = color;
            highlightColor.a = alpha;
            coloredTile.color = Color.white; // Use white on tile so tilemap color controls it
            
            highlightTilemap.SetTile(cellPos, coloredTile);
            highlightTilemap.SetColor(cellPos, highlightColor); // Apply color with alpha to tilemap
        }
    }

    // ========================================================================
    // ======================== PUBLIC API FOR DEVS ===========================
    // ========================================================================

    /// <summary>
    /// Enables or disables hover highlighting.
    /// </summary>
    public void SetHoverEnabled(bool enabled) {
        hoverEnabled = enabled;
        if (!enabled && lastHoveredPos.x != -999 && !persistentHighlights.Contains(lastHoveredPos)) {
            // Clear current hover highlight when disabling
            highlightTilemap.SetTile(lastHoveredPos, null);
            lastHoveredPos = new Vector3Int(-999, -999, 0);
        }
    }

    /// <summary>
    /// Highlights a single tile persistently (won't disappear on hover exit).
    /// Useful for showing movement range, attack range, etc.
    /// </summary>
    public void HighlightCell(Vector3Int cellPos, Color color, float alpha = 0.5f) {
        if (!isInitialized || highlightTileAsset == null) return;
        
        persistentHighlights.Add(cellPos);
        SetHighlightAtCell(cellPos, color, alpha);
    }

    /// <summary>
    /// Highlights multiple tiles persistently.
    /// </summary>
    public void HighlightCells(IEnumerable<Vector3Int> cells, Color color, float alpha = 0.5f) {
        if (!isInitialized || highlightTileAsset == null) return;
        
        foreach (var cell in cells) {
            HighlightCell(cell, color, alpha);
        }
    }

    /// <summary>
    /// Clears a single highlighted tile.
    /// </summary>
    public void ClearHighlightAtCell(Vector3Int cellPos) {
        if (!isInitialized) return;
        
        persistentHighlights.Remove(cellPos);
        highlightTilemap.SetTile(cellPos, null);
        highlightTilemap.SetColor(cellPos, Color.white);
    }

    /// <summary>
    /// Clears all persistent highlights (keeps hover).
    /// </summary>
    public void ClearAllHighlights() {
        if (!isInitialized) return;
        
        foreach (var cell in persistentHighlights) {
            highlightTilemap.SetTile(cell, null);
            highlightTilemap.SetColor(cell, Color.white);
        }
        persistentHighlights.Clear();
    }

    /// <summary>
    /// Returns a hashset of all currently highlighted cell positions (persistent only, excludes hover).
    /// </summary>
    public HashSet<Vector3Int> GetHighlightedCells() {
        return new HashSet<Vector3Int>(persistentHighlights);
    }

    /// <summary>
    /// Gets the currently hovered cell position, or (-999, -999, 0) if none.
    /// </summary>
    public Vector3Int GetHoveredCell() {
        return lastHoveredPos;
    }

    // Legacy compatibility methods
    public void SetHoverColor(Color color) {
        hoverHighlightColor = color;
    }

    public void SetUseColliders(bool use) {
        // Kept for compatibility but colliders are always used now
    }
}

}
