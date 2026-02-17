using UnityEngine;
using NetFlower.UI;

namespace NetFlower {
    public class VisualDemo : MonoBehaviour {
        [SerializeField] private GridMap gridMap;
        [SerializeField] private Agent agent;
        [SerializeField] private float moveInterval = 1f;

        private float moveTimer = 0f;
        
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

        void Start() {
            // Auto-find GridMap if not assigned
            if (gridMap == null) {
                gridMap = GetComponent<GridMap>();
                if (gridMap == null) {
                    Debug.LogError("VisualDemo: GridMap not found! Attach this to the Grid GameObject or assign GridMap.");
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
        }

        void Update() {
            WalkDemo();         // Comment this out if you want to test only the intro sequence
            // SomeOtherDemo(); // Uncomment to test additional visual functionalities
        }

        void SomeOtherDemo() {
            // Placeholder for additional visual functionalities (e.g., highlighting movement range, etc.)
        }

        void WalkDemo() {
            // This demo will move the agent around the map in a simple pattern
            // demonstrating the visual updates.
            
            if (agent == null || gridMap == null) return;

            moveTimer += Time.deltaTime;

            if (moveTimer >= moveInterval) {
                moveTimer = 0f;

                // Handle intro sequence first
                if (inIntroSequence) {
                    if (introIndex < introPositions.Length) {
                        Vector2Int introPos = introPositions[introIndex];
                        
                        // Check if position is within bounds
                        if (gridMap.InBounds(introPos)) {
                            
                            Vector2Int tilemapCoord = gridMap.MapIndexToTilemap(introPos);
                            bool isWalkable = gridMap.IsWalkable(introPos);
                            
                            Debug.Log($"Intro sequence: Moving to map index {introPos} (tilemap coord {tilemapCoord}), walkable={isWalkable}");
                            gridMap.TryMoveAgentByMapIndex(agent, introPos);
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
                Vector2Int? currentPos = gridMap.GetAgentMapIndex(agent);
                if (!currentPos.HasValue) return;
                
                // Try to move to adjacent walkable tile (simple right/down pattern)
                Vector2Int nextPos = currentPos.Value;
                Vector2Int mapDimensions = gridMap.GetMapDimensions();
                
                // Try right first
                if (nextPos.x + 1 < mapDimensions.x && gridMap.IsWalkable(new Vector2Int(nextPos.x + 1, nextPos.y))) {
                    nextPos = new Vector2Int(nextPos.x + 1, nextPos.y);
                }
                // Try down
                else if (nextPos.y + 1 < mapDimensions.y && gridMap.IsWalkable(new Vector2Int(nextPos.x, nextPos.y + 1))) {
                    nextPos = new Vector2Int(nextPos.x, nextPos.y + 1);
                }
                // Wrap back to start
                else {
                    // Find first walkable tile again
                    if (gridMap.TryGetFirstWalkableTile(out Tile firstWalkableTile)) {
                        nextPos = firstWalkableTile.Position;
                    }
                }
                
                gridMap.TryMoveAgentByMapIndex(agent, nextPos);
            }
        }
    }
}
