/**********************************************************************
 * File         : NPCBehavior.cs
 * Author       : John Shi
 * Date Created : 2026-03-17
 * Description  : Defines generic NPC behavior for agents. To make an agent an NPC add this script as a component.
                  TODO: Implement more complex AI for different archtypes (tank, healer, etc.)
 **********************************************************************/
 
using UnityEngine;
using System.Collections.Generic;
using NetFlower.UI;
using System.Linq;

namespace NetFlower {

    /// <summary>
    /// Basic NPC behavior controller. Attach to an Agent to give it simple autonomous decision-making.
    /// </summary>
    public class NPCBehavior : MonoBehaviour {

        [Header("NPC Settings")]
        [SerializeField] private bool isNPC = true;
        [SerializeField] private float decisionDelaySeconds = 1f; // Delay before making a decision

        private Agent agent;
        private float actionTimer = 0f;
        private bool hasActedThisTurn = false;
        private GridMap gridMap;

        private void Start() {
            agent = GetComponent<Agent>();
            if (agent == null) {
                Debug.LogError("NPCBehavior: Agent component not found on this GameObject!");
                enabled = false;
            }
        }

        /// <summary>
        /// Returns true if this is an NPC-controlled agent.
        /// </summary>
        public bool IsNPC => isNPC;

        /// <summary>
        /// Resets the action flag at the start of each turn.
        /// </summary>
        public void OnTurnStart() {
            hasActedThisTurn = false;
            actionTimer = decisionDelaySeconds; // Small delay before first decision
        }

        /// <summary>
        /// Updates NPC behavior during the turn. Call this from BattleManager's Update() when it's an NPC's turn.
        /// </summary>
        public void UpdateAI(GridMap gridMap, BattleManager battleManager) {
            if (!isNPC || hasActedThisTurn || agent == null || gridMap == null) return;

            if (gridMap.MapManager == null || gridMap.MapManager.ActiveMap == null) return;

            actionTimer -= Time.deltaTime;
            if (actionTimer > 0f) return; // Wait before acting

            hasActedThisTurn = true;

            // Simple AI logic: Try to move towards enemies, then use abilities
            DecideAction(gridMap, battleManager);
        }

        /// <summary>
        /// Main decision-making logic for the NPC.
        /// </summary>
        private void DecideAction(GridMap gridMap, BattleManager battleManager) {
            Map map = gridMap.MapManager.ActiveMap;
            int actionCount = 0;
            const int maxActionsPerTurn = 10; // Prevent infinite loops
 
            while (actionCount < maxActionsPerTurn) {
                Tile currentTile = map.GetCurrentTile(agent);
 
                if (currentTile == null) {
                    Debug.LogWarning($"NPCBehavior: {agent.Name} is not on the map!");
                    break;
                }
 
                // Priority 1: Try to use an ability on an enemy
                if (TryUseAbilityOnEnemy(gridMap, battleManager, map, currentTile)) {
                    actionCount++;
                    Debug.Log($"NPCBehavior: {agent.Name} used an ability (action {actionCount}).");
                    continue; // Try another action
                }
 
                // Priority 2: Move towards the nearest enemy
                if (TryMoveTowardEnemy(gridMap, map, currentTile)) {
                    actionCount++;
                    Debug.Log($"NPCBehavior: {agent.Name} moved (action {actionCount}).");
                    continue; // Try another action
                }
 
                // No more beneficial actions available
                break;
            }
 
            if (actionCount == 0) {
                Debug.Log($"NPCBehavior: {agent.Name} has no beneficial actions available.");
            } else {
                Debug.Log($"NPCBehavior: {agent.Name} completed {actionCount} action(s), ending turn.");
            }
 
            battleManager.OnEndTurnPressed();
        }

        /// <summary>
        /// Attempts to use an ability on an enemy within range.
        /// Returns true if an ability was used.
        /// </summary>
        private bool TryUseAbilityOnEnemy(GridMap gridMap, BattleManager battleManager, Map map, Tile currentTile) {
            List<Ability> abilities = agent.GetAbilities();

            // Filter for abilities that are available (not on cooldown)
            List<Ability> availableAbilities = new List<Ability>();
            foreach (var ability in abilities) {
                if (agent.CanUseAbility(ability)) {
                    availableAbilities.Add(ability);
                }
            }

            if (availableAbilities.Count == 0) {
                return false;
            }

            // Try each available ability to find a target
            foreach (var ability in availableAbilities) {
                // Get all valid targets for this ability within range
                List<Tile> validTargets = GetValidAbilityTargets(agent, ability, currentTile, map);

                // Filter for tiles with enemies
                List<Tile> enemyTargets = new List<Tile>();
                foreach (var tile in validTargets) {
                    Agent targetAgent = map.GetAgentAtTile(tile);
                    if (targetAgent != null && IsEnemy(agent, targetAgent)) {
                        enemyTargets.Add(tile);
                    }
                }

                // If we found enemy targets, use the ability on the closest one
                if (enemyTargets.Count > 0) {
                    Tile targetTile = GetClosestTile(currentTile, enemyTargets);
                    
                    var context = new AbilityUseContext {
                        Ability = ability,
                        Caster = agent,
                        TargetTile = targetTile,
                        TurnNumber = 0 // This would need to be passed in or accessed from BattleManager
                    };

                    agent.UseAbility(context);
                    Debug.Log($"NPCBehavior: {agent.Name} used {ability.DisplayName} on tile {targetTile.Position}");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Attempts to move towards the nearest enemy within movement range.
        /// Returns true if movement was executed.
        /// </summary>
        private bool TryMoveTowardEnemy(GridMap gridMap, Map map, Tile currentTile) {
            // Get all tiles the agent can move to
            List<Tile> movableTiles = map.GetMovableTiles(agent);
            if (movableTiles.Count == 0) {
                return false;
            }

            // Find the nearest enemy on the map
            Agent nearestEnemy = FindNearestEnemy(map);
            if (nearestEnemy == null) {
                Debug.Log($"NPCBehavior: {agent.Name} found no enemies to move toward.");
                return false;
            }

            Tile enemyTile = map.GetCurrentTile(nearestEnemy);
            if (enemyTile == null) {
                return false;
            }

            // Find the movable tile closest to the enemy
            Tile bestMoveTile = GetClosestTile(enemyTile, movableTiles);

            if (bestMoveTile != null) {
                // Calculate the movement cost
                var path = map.FindShortestPath(currentTile.Position, bestMoveTile.Position);
                int pathLength = (path != null && path.Count > 0) ? path.Count - 1 : 0;

                // Move the agent
                gridMap.TryMoveAgentByMapIndex(agent, bestMoveTile.Position);
                if (pathLength > 0) {
                    agent.SpendMovement(pathLength);
                }

                Debug.Log($"NPCBehavior: {agent.Name} moved toward {nearestEnemy.Name} at {bestMoveTile.Position}");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Finds the nearest enemy agent to this NPC on the map.
        /// </summary>
        private Agent FindNearestEnemy(Map map) {
            Agent nearestEnemy = null;
            float minDistance = float.MaxValue;

            Tile currentTile = map.GetCurrentTile(agent);
            if (currentTile == null) return null;

            // Get all registered agents on the map using the public API
            var registeredAgents = map.GetRegisteredAgents();
            
            foreach (var otherAgent in registeredAgents) {
                // Skip self and allies
                if (otherAgent == agent || !IsEnemy(agent, otherAgent)) {
                    continue;
                }

                Tile otherTile = map.GetCurrentTile(otherAgent);
                if (otherTile == null) continue;

                // Calculate distance
                float distance = Vector2Int.Distance(currentTile.Position, otherTile.Position);
                if (distance < minDistance) {
                    minDistance = distance;
                    nearestEnemy = otherAgent;
                }
            }

            return nearestEnemy;
        }

        /// <summary>
        /// Checks if two agents are enemies (on different teams).
        /// </summary>
        private bool IsEnemy(Agent agentA, Agent agentB) {
            if (agentA == null || agentB == null) return false;

            string teamA = agentA.ParentName;
            string teamB = agentB.ParentName;

            // They're enemies if on different teams
            return teamA != teamB;
        }

        /// <summary>
        /// Gets all tiles within range of the ability from the caster's position.
        /// </summary>
        private List<Tile> GetValidAbilityTargets(Agent caster, Ability ability, Tile casterTile, Map map) {
            List<Tile> validTargets = new List<Tile>();

            for (int x = 0; x < map.Width; x++) {
                for (int y = 0; y < map.Height; y++) {
                    Vector2Int pos = new Vector2Int(x, y);
                    Tile tile = map.GetTileAtPosition(pos);
                    if (tile == null) continue;

                    // Check range
                    float distance = Vector2Int.Distance(casterTile.Position, pos);
                    if (distance < ability.RangeMin || distance > ability.RangeMax) continue;

                    // Check if walkable
                    if (!tile.IsWalkable) continue;

                    validTargets.Add(tile);
                }
            }

            return validTargets;
        }

        /// <summary>
        /// Finds the tile in the list closest to a reference tile.
        /// </summary>
        private Tile GetClosestTile(Tile referenceTile, List<Tile> tiles) {
            if (tiles.Count == 0) return null;

            Tile closest = tiles[0];
            float minDistance = Vector2Int.Distance(referenceTile.Position, tiles[0].Position);

            for (int i = 1; i < tiles.Count; i++) {
                float distance = Vector2Int.Distance(referenceTile.Position, tiles[i].Position);
                if (distance < minDistance) {
                    minDistance = distance;
                    closest = tiles[i];
                }
            }

            return closest;
        }
    }
}