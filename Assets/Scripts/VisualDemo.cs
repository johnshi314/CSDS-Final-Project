using UnityEngine;
using NetFlower;
using NetFlower.UI;

public class VisualDemo : MonoBehaviour
{
    [SerializeField] private GridUI gridUI;
    [SerializeField] private Agent agent;
    [SerializeField] private float moveInterval = 1f;

    private float moveTimer = 0f;
    private bool isRegistered = false;
    
    // Intro sequence to show specific positions
    private bool inIntroSequence = true;
    private int introIndex = 0;
    private Vector2Int[] introPositions = new Vector2Int[] {
        new Vector2Int(0, 0),
        new Vector2Int(0, 1),
        new Vector2Int(0, 2),
        new Vector2Int(0, 3),
        new Vector2Int(0, 4)
    };

    void Start()
    {
        // Auto-find GridUI if not assigned
        if (gridUI == null) {
            gridUI = GetComponent<GridUI>();
            if (gridUI == null) {
                Debug.LogError("VisualDemo: GridUI not found! Attach this to the Grid GameObject or assign GridUI.");
                return;
            }
        }

        // Auto-find agent if not assigned
        if (agent == null) {
            agent = FindFirstObjectByType<Agent>();
            if (agent == null) {
                Debug.LogError("VisualDemo: No Agent found in scene!");
                return;
            }
        }

        // Wait a frame for GridUI to initialize
        Invoke(nameof(RegisterAgentDelayed), 0.1f);
    }

    void RegisterAgentDelayed() {
        // Log the map bounds for debugging
        Debug.Log($"Map dimensions: {gridUI.map.Width}x{gridUI.map.Height}");
        Debug.Log($"Map index (0,0) corresponds to tilemap coordinate: {gridUI.MapIndexToTilemap(Vector2Int.zero)}");
        Debug.Log($"Map index ({gridUI.map.Width-1},{gridUI.map.Height-1}) corresponds to tilemap coordinate: {gridUI.MapIndexToTilemap(new Vector2Int(gridUI.map.Width-1, gridUI.map.Height-1))}");
        
        // Find a walkable tile to start on
        Vector2Int startPos = Vector2Int.zero;
        bool foundWalkable = false;
        
        for (int y = 0; y < gridUI.map.Height && !foundWalkable; y++) {
            for (int x = 0; x < gridUI.map.Width && !foundWalkable; x++) {
                if (gridUI.map.Tiles[x, y].IsWalkable) {
                    startPos = new Vector2Int(x, y);
                    foundWalkable = true;
                    Debug.Log($"Found first walkable tile at map index {startPos}, tilemap coord: {gridUI.MapIndexToTilemap(startPos)}");
                }
            }
        }
        
        if (!foundWalkable) {
            Debug.LogError("No walkable tiles found on the map!");
            return;
        }
        
        // Register the agent at the first walkable tile
        gridUI.RegisterAgentByMapIndex(agent, startPos);
        isRegistered = true;
        Debug.Log("VisualDemo: Agent registered and ready to move");
    }

    void Update()
    {
        if (!isRegistered || agent == null || gridUI == null) return;

        moveTimer += Time.deltaTime;

        if (moveTimer >= moveInterval) {
            moveTimer = 0f;

            // Handle intro sequence first
            if (inIntroSequence) {
                if (introIndex < introPositions.Length) {
                    Vector2Int introPos = introPositions[introIndex];
                    
                    // Check if position is within bounds
                    if (introPos.x >= 0 && introPos.x < gridUI.map.Width && 
                        introPos.y >= 0 && introPos.y < gridUI.map.Height) {
                        
                        Vector2Int tilemapCoord = gridUI.MapIndexToTilemap(introPos);
                        bool isWalkable = gridUI.map.Tiles[introPos.x, introPos.y].IsWalkable;
                        
                        Debug.Log($"Intro sequence: Moving to map index {introPos} (tilemap coord {tilemapCoord}), walkable={isWalkable}");
                        gridUI.MoveAgentByMapIndex(agent, introPos);
                    } else {
                        Debug.LogWarning($"Intro position {introPos} is out of bounds!");
                    }
                    
                    introIndex++;
                } else {
                    Debug.Log("Intro sequence complete! Starting normal movement...");
                    inIntroSequence = false;
                }
                return;
            }

            // Normal movement pattern (after intro)
            Vector2Int? currentPos = gridUI.GetAgentMapIndex(agent);
            if (!currentPos.HasValue) return;
            
            // Try to move to adjacent walkable tile (simple right/down pattern)
            Vector2Int nextPos = currentPos.Value;
            
            // Try right first
            if (nextPos.x + 1 < gridUI.map.Width && gridUI.map.Tiles[nextPos.x + 1, nextPos.y].IsWalkable) {
                nextPos = new Vector2Int(nextPos.x + 1, nextPos.y);
            }
            // Try down
            else if (nextPos.y + 1 < gridUI.map.Height && gridUI.map.Tiles[nextPos.x, nextPos.y + 1].IsWalkable) {
                nextPos = new Vector2Int(nextPos.x, nextPos.y + 1);
            }
            // Wrap back to start
            else {
                // Find first walkable tile again
                for (int y = 0; y < gridUI.map.Height; y++) {
                    for (int x = 0; x < gridUI.map.Width; x++) {
                        if (gridUI.map.Tiles[x, y].IsWalkable) {
                            nextPos = new Vector2Int(x, y);
                            goto found;
                        }
                    }
                }
                found:;
            }
            
            gridUI.MoveAgentByMapIndex(agent, nextPos);
        }
    }
}
