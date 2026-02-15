using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Loads tile walkability data from a tilemap overlay.
/// </summary>
public static class WalkabilityLoader {

    /// <summary>
    /// Result of loading walkability from tilemap, including coordinate offset information.
    /// </summary>
    public class WalkabilityData {
        public bool[,] Walkability;
        public Vector2Int BoundsMin; // The minimum coordinate of the tilemap (could be negative)
        public int Width;
        public int Height;
    }

    /// <summary>
    /// Loads walkability from a tilemap overlay. Any position where the walkableTilemap
    /// has a tile AND the baseTilemap has a tile is marked as walkable.
    /// </summary>
    public static WalkabilityData LoadFromTilemap(Tilemap baseTilemap, Tilemap walkableTilemap) {
        if (baseTilemap == null) {
            Debug.LogError("Base tilemap is null");
            return null;
        }
        if (walkableTilemap == null) {
            Debug.LogError("Walkable tilemap is null");
            return null;
        }

        // Get bounds of the base tilemap
        BoundsInt bounds = baseTilemap.cellBounds;
        
        if (bounds.size.x <= 0 || bounds.size.y <= 0) {
            Debug.LogError("Base tilemap has no tiles");
            return null;
        }

        int width = bounds.size.x;
        int height = bounds.size.y;
        bool[,] walkability = new bool[width, height];

        // Iterate through all cells in the base tilemap bounds
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                Vector3Int cellPos = new Vector3Int(bounds.xMin + x, bounds.yMin + y, 0);
                
                // A tile is walkable if BOTH tilemaps have a tile at this position
                bool hasBaseTile = baseTilemap.HasTile(cellPos);
                bool hasWalkableTile = walkableTilemap.HasTile(cellPos);
                
                walkability[x, y] = hasBaseTile && hasWalkableTile;
                
                // // Debug first row to see what's happening
                // if (y == 0 && x < 10) {
                //     Debug.Log($"Map index ({x},{y}) = tilemap coord {cellPos}: baseTile={hasBaseTile}, walkableTile={hasWalkableTile}, walkable={walkability[x, y]}");
                // }
            }
        }

        Debug.Log($"Loaded walkability map from tilemap: {width}x{height}, offset: ({bounds.xMin}, {bounds.yMin})");
        
        return new WalkabilityData {
            Walkability = walkability,
            BoundsMin = new Vector2Int(bounds.xMin, bounds.yMin),
            Width = width,
            Height = height
        };
    }
}
