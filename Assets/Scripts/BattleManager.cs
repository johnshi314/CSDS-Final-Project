/**********************************************************************
 * File         : BattleManager.cs
 * Author       : Mikey Maldonado
 * Date Created : 2026-02-05
 * Description  : Manages the flow of a turn-based battle, including turn order, player actions, and state transitions.
 *                TODO: Refactor to use TurnOrder and other classes

 * Last Modified: 2026-03-19
 * Last Modified By: John Shi
 * Note: I modified the BattleManager so it now supports NPC agents.
 **********************************************************************/
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;
using NetFlower.UI;


namespace NetFlower {

    /// <summary>
    /// States for the battle flow. Determines what player input does and what UI is shown.
    /// </summary>
    public enum BattleState {
        NotStarted,
        WaitingForAction,
        SelectingMoveTile,
        SelectingAbility,
        SelectingAbilityTarget,
        MovingAgent,
        WaitingForAnimations
    }

    /// <summary>
    /// Drives a turn-based battle demo. Owns the state machine that decides
    /// what mouse clicks do, cycles turns between agents, and draws IMGUI
    /// buttons for Move / End Turn / Cancel.
    ///
    /// NOW SUPPORTS NPC AGENTS - Agents with NPCBehavior will move automatically.
    /// </summary>
    public class BattleManager : MonoBehaviour {

        // Timer variables
        protected float turnTimer = 30f;
        private const float TURN_TIME_LIMIT = 30f;
        protected bool timerActive = false;

        [Header("Turn Management")]
        public int currentTurn = 0; // Tracks the current turn number (starting from 0)

        [Header("References")]
        [SerializeField] private GridMap gridMap;

        [Header("Tile Highlighting")]
        [SerializeField] private Color moveRangeColor = new Color(0.2f, 0.6f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float moveRangeAlpha = 0.5f;
        [SerializeField] private Color abilityTargetColor = new Color(1f, 0.5f, 0.2f, 1f);
        [SerializeField, Range(0f, 1f)] private float abilityTargetAlpha = 0.5f;
        [SerializeField] private Color abilityAoePreviewColor = new Color(0.9f, 0.1f, 0.1f, 1f);
        [SerializeField, Range(0f, 1f)] private float abilityAoePreviewAlpha = 0.45f;

        // UI GUI switched added
        [Header("UI Options")]
        [SerializeField] private bool useCanvasUI = true;

        private Match match; // Reference to the Match component for recording stats


        // ---------- for UI read-only access ----------
        public IReadOnlyList<Ability> AvailableAbilitiesForUI => availableAbilities.AsReadOnly();
        public int SelectedAbilityIndexForUI => selectedAbilityIndex;
        public Ability SelectedAbilityForUI => selectedAbility;
        public float TurnTimerForUI => turnTimer;
        public Rect UiRectForIMGUI => uiRect; // for debugging
        // --------------------------------------------

        // State
        private BattleState state = BattleState.NotStarted;
        protected List<Agent> turnOrder = new List<Agent>();
        protected int currentAgentIndex;
        private List<Tile> validMoveTiles = new List<Tile>();
        private readonly Dictionary<Agent, string> networkUnitIdsByAgent = new Dictionary<Agent, string>();
        private readonly Dictionary<string, Agent> agentsByNetworkUnitId = new Dictionary<string, Agent>(StringComparer.Ordinal);

        // Ability selection state
        private List<Ability> availableAbilities = new List<Ability>();
        private int selectedAbilityIndex = 0;
        private Ability selectedAbility = null;  // The ability currently being targeted
        private List<Tile> validAbilityTargets = new List<Tile>();  // Valid target tiles for the selected ability
        private Tile lastHoveredAbilityTargetTile = null; // For AoE preview highlighting

        // Movement tweening state
        [Header("Movement Animation")]
        [SerializeField] private float moveSpeed = 5f; // tiles per second
        private List<Vector3> movePath;
        private int movePathIndex;
        private float moveLerpT;
        private Agent movingAgent;

        // GUI rect used to block tile clicks over the UI panel
        [Header("UI Rect")]
        [SerializeField, Range(0f, 1f)] public float scaleWidth = 0.5f;
        private Rect uiRect = new Rect(5, 5, 350, 400);

        // NPC behavior tracking
        private NPCBehavior currentNPCBehavior;

        public BattleState State => state;
        public Agent CurrentAgent =>
            (turnOrder.Count > 0 && currentAgentIndex < turnOrder.Count)
                ? turnOrder[currentAgentIndex]
                : null;

        //Stats
        private MatchStats matchstats;
        private MatchupStats currentMatchup;

        public void Start() {
            // For Mach I need:
            // - A ID
            // - B ID
            // Winner char name
            // Match ID
            //match = GetComponent<Match>();
            match = Match.GetInstance();

            if (match == null) {
                GameObject matchObj = new GameObject("Match");
                match = matchObj.AddComponent<Match>();
            }

            Debug.Log("Match ID");
            Debug.Log(match.dbMatchId);

            matchstats = match.StartMatch(match.dbMatchId);
            Debug.Log(matchstats.ToJson());

        }

        // ------------------------------------------------------------------ //
        // Public API (called by GameplayDemo or wired to Canvas buttons)
        // ------------------------------------------------------------------ //

        /// <param name="deferFirstBeginTurn">If true, wait for server (see <see cref="OnlineBattleManager"/>) before the first BeginTurn.</param>
        public void StartBattle(bool deferFirstBeginTurn = false) {
            if (gridMap == null) {
                gridMap = GetComponent<GridMap>();
                if (gridMap == null)
                    gridMap = GetComponentInParent<GridMap>();
            }

            if (gridMap == null || gridMap.MapManager == null) {
                Debug.LogError("BattleManager: GridMap or MapManager not available.");
                return;
            }

            // Interleave red/blue so turns alternate when both sides have a unit at that wave index.
            // Uneven rosters (e.g. 1v5) are supported: extra members of the larger team fill later slots.
            turnOrder.Clear();
            int max = Mathf.Max(gridMap.RedAgents.Count, gridMap.BlueAgents.Count);
            for (int i = 0; i < max; i++) {
                if (i < gridMap.RedAgents.Count && gridMap.RedAgents[i] != null)
                    turnOrder.Add(gridMap.RedAgents[i]);

                if (i < gridMap.BlueAgents.Count && gridMap.BlueAgents[i] != null)
                    turnOrder.Add(gridMap.BlueAgents[i]);
            }

            if (turnOrder.Count == 0) {
                Debug.LogError("BattleManager: No agents found for battle.");
                return;
            }

            BuildNetworkUnitIdMap();

            // record matchup stats for 1 v 1 matches
            if (turnOrder.Count == 2) {
                string charA = turnOrder[0].Name;
                string charB = turnOrder[1].Name;

                currentMatchup = match.RegisterMatchup(charA, charB);

                Debug.Log($"[BattleManager] Matchup registered: {charA} vs {charB}");
            }

            currentAgentIndex = 0;
            Debug.Log($"BattleManager: Battle started with {turnOrder.Count} agents.");
            if (deferFirstBeginTurn)
                return;
            BeginTurn();
        }

        /// <summary>
        /// Online: build <c>spawns|n|x0|y0|...</c> from current <see cref="turnOrder"/> tile positions (must match server slot order).
        /// </summary>
        public bool TryBuildTurnSlotSpawnHandshakeLine(out string line) {
            line = null;
            if (turnOrder == null || turnOrder.Count == 0 || gridMap?.MapManager?.ActiveMap == null)
                return false;
            var map = gridMap.MapManager.ActiveMap;
            var sb = new StringBuilder();
            sb.Append("spawns|").Append(turnOrder.Count);
            for (int i = 0; i < turnOrder.Count; i++) {
                var agent = turnOrder[i];
                if (agent == null) return false;
                var tile = map.GetCurrentTile(agent);
                if (tile == null) return false;
                sb.Append('|').Append(tile.Position.x).Append('|').Append(tile.Position.y);
            }
            line = sb.ToString();
            return true;
        }

        /// <summary>
        /// Online: apply server-broadcast spawn positions for each turn-order slot (unregister then place).
        /// </summary>
        public bool TryApplyServerSpawnLayoutByTurnSlot(IReadOnlyList<Vector2Int> positionsByTurnSlot) {
            if (gridMap?.MapManager?.ActiveMap == null || turnOrder == null || positionsByTurnSlot == null)
                return false;
            if (positionsByTurnSlot.Count != turnOrder.Count) {
                Debug.LogError($"[BattleManager] Spawn layout length {positionsByTurnSlot.Count} != turn order {turnOrder.Count}.");
                return false;
            }
            var map = gridMap.MapManager.ActiveMap;
            foreach (var agent in turnOrder) {
                if (agent != null)
                    map.UnregisterAgent(agent);
            }
            for (int i = 0; i < turnOrder.Count; i++) {
                var agent = turnOrder[i];
                if (agent == null) continue;
                var pos = positionsByTurnSlot[i];
                if (!gridMap.MapManager.PlaceAgent(agent, pos)) {
                    Debug.LogError($"[BattleManager] Server spawn failed for slot {i} {agent.Name} at {pos}.");
                    return false;
                }
            }
            gridMap.RefreshAgentWorldPositionsFromMap();
            return true;
        }

        /// <summary>True if this is offline battle or the local human may act on the current agent (online).</summary>
        public bool LocalPlayerMayControlTurn() {
            var online = GetComponent<OnlineBattleManager>();
            if (online == null) return true;
            return online.LocalMayControlCurrentAgent();
        }

        public void OnMovePressed() {
            if (state != BattleState.WaitingForAction) return;
            if (!LocalPlayerMayControlTurn()) return;

            Agent agent = CurrentAgent;
            if (agent == null) return;

            // For accessing agent's playerMatchStats object
            if (CurrentAgent.playerMatchStats.matchId == 0) {
                CurrentAgent.playerMatchStats = CurrentAgent.RegisterPlayer(match.dbMatchId);
            }

            validMoveTiles = gridMap.MapManager.ActiveMap.GetMovableTiles(agent);
            if (validMoveTiles.Count == 0) {
                Debug.Log($"BattleManager: {agent.Name} has no valid moves.");
                return;
            }

            state = BattleState.SelectingMoveTile;
            gridMap.HighlightTiles(validMoveTiles, moveRangeColor, moveRangeAlpha);
            Debug.Log($"BattleManager: Showing {validMoveTiles.Count} move options for {agent.Name}.");
        }

        public void OnCancelPressed() {
            if (state != BattleState.SelectingMoveTile) return;

            state = BattleState.WaitingForAction;
            gridMap.ClearHighlights();
            validMoveTiles.Clear();
            Debug.Log("BattleManager: Move cancelled.");
        }

        public void OnUseAbilityPressed() {
            if (state != BattleState.WaitingForAction) return;
            if (!LocalPlayerMayControlTurn()) return;

            Agent agent = CurrentAgent;
            if (agent == null) return;

            // Get all abilities for the current agent
            availableAbilities = agent.GetAbilities();
            if (availableAbilities.Count == 0) {
                Debug.Log($"BattleManager: {agent.Name} has no abilities.");
                return;
            }

            selectedAbilityIndex = 0;
            state = BattleState.SelectingAbility;
            Debug.Log($"BattleManager: Showing {availableAbilities.Count} abilities for {agent.Name}.");
        }

        public void OnAbilityCancelled() {
            if (state != BattleState.SelectingAbility) return;

            state = BattleState.WaitingForAction;
            availableAbilities.Clear();
            selectedAbilityIndex = 0;
            Debug.Log("BattleManager: Ability selection cancelled.");
        }

        public void OnPreviousAbilityPressed() {
            if (state != BattleState.SelectingAbility) return;
            selectedAbilityIndex = (selectedAbilityIndex - 1 + availableAbilities.Count) % availableAbilities.Count;
        }

        public void OnNextAbilityPressed() {
            if (state != BattleState.SelectingAbility) return;
            selectedAbilityIndex = (selectedAbilityIndex + 1) % availableAbilities.Count;
        }

        public void OnConfirmAbilityPressed() {
            if (state != BattleState.SelectingAbility || availableAbilities.Count == 0) return;
            if (!LocalPlayerMayControlTurn()) return;

            Agent agent = CurrentAgent;
            selectedAbility = availableAbilities[selectedAbilityIndex];

            // Check if ability can be used
            if (!agent.CanUseAbility(selectedAbility)) {
                Debug.LogWarning($"BattleManager: {agent.Name} cannot use {selectedAbility.DisplayName} (on cooldown or unavailable).");
                return;
            }

            // If ability is Global mode, apply immediately to all valid targets
            if (selectedAbility.TargetMode == AbilityTargetMode.Global) {
                Map map = gridMap.MapManager.ActiveMap;
                // For global abilities, apply to caster's tile
                Tile casterTile = map.GetCurrentTile(agent);
                if (casterTile != null) {
                    var aux = new AbilityUseContext {
                        Ability = selectedAbility,
                        Caster = agent,
                        TargetTile = casterTile,
                        TurnNumber = currentTurn
                    };
                    agent.UseAbility(aux);
                    Debug.Log($"BattleManager: {agent.Name} used {selectedAbility.DisplayName} (Global).");
                    OnAfterLocalAbilityUsed(selectedAbility, casterTile);
                }
                state = BattleState.WaitingForAction;
                availableAbilities.Clear();
                selectedAbility = null;
                selectedAbilityIndex = 0;
            }
            // If ability is Point mode, go to target selection
            else if (selectedAbility.TargetMode == AbilityTargetMode.Point) {
                Map map = gridMap.MapManager.ActiveMap;
                Tile casterTile = map.GetCurrentTile(agent);

                // Get all valid target tiles based on ability range
                validAbilityTargets = GetValidAbilityTargets(agent, selectedAbility, casterTile);
                if (validAbilityTargets.Count == 0) {
                    Debug.LogWarning($"BattleManager: {agent.Name} has no valid targets for {selectedAbility.DisplayName}.");
                    state = BattleState.WaitingForAction;
                    selectedAbility = null;
                    return;
                }

                state = BattleState.SelectingAbilityTarget;
                gridMap.HighlightTiles(validAbilityTargets, abilityTargetColor, abilityTargetAlpha);
                Debug.Log($"BattleManager: Showing {validAbilityTargets.Count} target options for {selectedAbility.DisplayName}.");
            }
        }

        private List<Tile> GetValidAbilityTargets(Agent caster, Ability ability, Tile casterTile) {
            List<Tile> validTargets = new List<Tile>();
            Map map = gridMap.MapManager.ActiveMap;

            // Get all tiles within range
            for (int x = 0; x < map.Width; x++) {
                for (int y = 0; y < map.Height; y++) {
                    Vector2Int pos = new Vector2Int(x, y);
                    Tile tile = map.GetTileAtPosition(pos);
                    if (tile == null) continue;

                    // Check range
                    float distance = Vector2Int.Distance(casterTile.Position, pos);
                    if (distance < ability.RangeMin || distance > ability.RangeMax) continue;

                    // Check if this tile is walkable
                    if (!tile.IsWalkable) continue;

                    validTargets.Add(tile);
                }
            }

            return validTargets;
        }

        public void OnAbilityTargetTileSelected(Tile targetTile) {
            if (state != BattleState.SelectingAbilityTarget || selectedAbility == null) return;
            if (!LocalPlayerMayControlTurn()) return;

            Agent agent = CurrentAgent;
            var aux = new AbilityUseContext {
                Ability = selectedAbility,
                Caster = agent,
                TargetTile = targetTile,
                TurnNumber = currentTurn
            };
            agent.UseAbility(aux);
            Debug.Log($"BattleManager: {agent.Name} used {selectedAbility.DisplayName} on tile {targetTile.Position}.");
            Animator animator = agent.GetComponentInChildren<Animator>();
            if (animator != null ) {
                animator.SetBool("Attack1",true);
                Debug.Log("Attack1 triggered");
            }

            OnAfterLocalAbilityUsed(selectedAbility, targetTile);

            gridMap.ClearHighlights();
            gridMap.ClearPreviewHighlights();
            validAbilityTargets.Clear();
            lastHoveredAbilityTargetTile = null;
            state = BattleState.WaitingForAction;
            availableAbilities.Clear();
            selectedAbility = null;
            selectedAbilityIndex = 0;
            // Do not advance turn automatically after using ability
        }

        public void OnAbilityTargetCancelled() {
            if (state != BattleState.SelectingAbilityTarget) return;

            gridMap.ClearHighlights();
            gridMap.ClearPreviewHighlights();
            validAbilityTargets.Clear();
            lastHoveredAbilityTargetTile = null;
            state = BattleState.SelectingAbility;
            selectedAbility = null;
            Debug.Log("BattleManager: Ability targeting cancelled.");
        }

        public void OnEndTurnPressed() {
            if (state == BattleState.NotStarted) return;
            if (state != BattleState.MovingAgent && state != BattleState.WaitingForAnimations && !LocalPlayerMayControlTurn())
                return;

            // If agent is mid-move animation, wait for it to finish before advancing
            if (state == BattleState.MovingAgent) {
                state = BattleState.WaitingForAnimations;
                gridMap.ClearHighlights();
                validMoveTiles.Clear();
                return;
            }

            gridMap.ClearHighlights();
            validMoveTiles.Clear();
            AdvanceTurn();
        }

        /// <summary>Offline: advance turn. Online: notify server (subclass overrides).</summary>
        protected virtual void AdvanceTurn() {
            if (CurrentAgent != null) {
                CurrentAgent.OnTurnEnd(currentTurn);
            }
            
            // Record turns taken
            if (CurrentAgent != null && CurrentAgent.playerMatchStats != null) {
                CurrentAgent.playerMatchStats.turnsTaken++;
            }
            
            currentAgentIndex = (currentAgentIndex + 1) % turnOrder.Count;
            if (currentAgentIndex == 0)
                currentTurn++;
            
            BeginTurn();
            // Do not stop timer here; BeginTurn restarts it for the next player
        }

        // ------------------------------------------------------------------ //
        // Internal turn flow
        // ------------------------------------------------------------------ //

        private void BeginTurn() {
            BeginTurn(TURN_TIME_LIMIT);
        }

        private void BeginTurn(float turnTimeSeconds) {
            state = BattleState.WaitingForAction;
            gridMap.ClearHighlights();
            validMoveTiles.Clear();
            validAbilityTargets.Clear();
            lastHoveredAbilityTargetTile = null;
            selectedAbility = null;
            availableAbilities.Clear();
            selectedAbilityIndex = 0;

            // Tick all agents' effects at the start of each turn
            foreach (var agent in turnOrder) {
                agent.TickEffects(currentTurn);
            }

            // Remove destroyed agents from turn order
            turnOrder.RemoveAll(agent => agent == null || !agent.gameObject.activeInHierarchy);
            
            // Check if one team has been completely wiped out
            CheckForVictoryCondition();
            
            if (CurrentAgent != null) {
                CurrentAgent.OnTurnStart(currentTurn);
                
                // Initialize NPC behavior if current agent is an NPC
                currentNPCBehavior = CurrentAgent.GetComponent<NPCBehavior>();
                if (currentNPCBehavior != null && currentNPCBehavior.IsNPC) {
                    currentNPCBehavior.OnTurnStart();
                    Debug.Log($"BattleManager: {CurrentAgent.Name} (NPC) is taking their turn. (Turn {currentTurn + 1})");
                } else {
                    Debug.Log($"BattleManager: {CurrentAgent.Name}'s turn. (Turn {currentTurn + 1})");
                }
            }

            // Start turn timer (overridden by server for online battles)
            turnTimer = turnTimeSeconds;
            timerActive = true;
        }

        /// <summary>Apply server turn index and sync counter; finalize previous agent like AdvanceTurn would.</summary>
        public void ApplyServerDirectedTurn(int nextSlot, int syncTurn, float turnTimeSeconds) {
            if (turnOrder.Count == 0) return;
            nextSlot = Mathf.Clamp(nextSlot, 0, turnOrder.Count - 1);

            if (state != BattleState.NotStarted && CurrentAgent != null) {
                CurrentAgent.OnTurnEnd(currentTurn);
                if (CurrentAgent.playerMatchStats != null)
                    CurrentAgent.playerMatchStats.turnsTaken++;
            }

            currentAgentIndex = nextSlot;
            currentTurn = syncTurn;
            BeginTurn(turnTimeSeconds);
        }

        /// <summary>Replay a peer move (map indices).</summary>
        public void ApplyRemoteMoveForSlot(int slot, int mapX, int mapY) {
            if (slot < 0 || slot >= turnOrder.Count || gridMap == null) return;
            var agent = turnOrder[slot];
            if (agent == null) return;
            ApplyRemoteMoveForAgent(agent, mapX, mapY);
        }

        /// <summary>Replay a peer ability use.</summary>
        public void ApplyRemoteAbilityForSlot(int slot, int abilityIndex, int mapX, int mapY) {
            if (slot < 0 || slot >= turnOrder.Count || gridMap == null) return;
            var agent = turnOrder[slot];
            if (agent == null) return;
            var abilities = agent.GetAbilities();
            if (abilityIndex < 0 || abilityIndex >= abilities.Count) return;
            var ability = abilities[abilityIndex];
            var map = gridMap.MapManager.ActiveMap;
            var tile = map.GetTileAtPosition(new Vector2Int(mapX, mapY));
            if (tile == null) return;
            var aux = new AbilityUseContext {
                Ability = ability,
                Caster = agent,
                TargetTile = tile,
                TurnNumber = currentTurn
            };
            agent.UseAbility(aux);
        }

        protected virtual void OnAfterLocalMoveCommitted(Vector2Int destinationMapIndex) { }

        protected virtual void OnAfterLocalAbilityUsed(Ability ability, Tile targetTile) { }

        protected bool TryGetNetworkUnitId(Agent agent, out string unitId) {
            unitId = null;
            return agent != null && networkUnitIdsByAgent.TryGetValue(agent, out unitId) && !string.IsNullOrEmpty(unitId);
        }

        public bool TryGetAgentByNetworkUnitId(string unitId, out Agent agent) {
            agent = null;
            return !string.IsNullOrEmpty(unitId) && agentsByNetworkUnitId.TryGetValue(unitId, out agent) && agent != null;
        }

        /// <summary>Replay a peer move (network unit id + map indices).</summary>
        public void ApplyRemoteMoveForUnit(string unitId, int mapX, int mapY) {
            if (string.IsNullOrEmpty(unitId) || gridMap == null) return;
            if (!TryGetAgentByNetworkUnitId(unitId, out var agent)) return;
            ApplyRemoteMoveForAgent(agent, mapX, mapY);
        }

        private void ApplyRemoteMoveForAgent(Agent agent, int mapX, int mapY) {
            var map = gridMap.MapManager.ActiveMap;
            var dest = new Vector2Int(mapX, mapY);
            Vector2Int? oldPos = map.GetCurrentTile(agent)?.Position;

            List<Vector3> worldPath = null;
            if (oldPos.HasValue) {
                var tilePath = map.FindShortestPath(oldPos.Value, dest);
                if (tilePath != null && tilePath.Count >= 2) {
                    float agentZ = agent.transform.position.z;
                    worldPath = new List<Vector3>(tilePath.Count);
                    foreach (var t in tilePath)
                        worldPath.Add(gridMap.MapIndexToWorldPosition(t.Position, agentZ));
                }
            }

            if (!gridMap.TryMoveAgentByMapIndex(agent, dest)) return;

            int pathLength = worldPath != null ? worldPath.Count - 1 : 0;
            if (pathLength > 0)
                agent.SpendMovement(pathLength);

            if (worldPath != null && worldPath.Count >= 2) {
                agent.transform.position = worldPath[0];
                StartCoroutine(AnimateRemoteMove(agent, worldPath));
            }
        }

        private IEnumerator AnimateRemoteMove(Agent agent, List<Vector3> path) {
            Animator animator = agent.GetComponentInChildren<Animator>();
            int idx = 0;
            float t = 0f;

            while (idx < path.Count - 1) {
                if (animator != null) {
                    animator.SetBool("IsWalking", true);
                    Vector3 dir = (path[idx] - path[idx + 1]).normalized;
                    animator.SetBool("IsLeft", dir.x >= 0);
                    animator.SetBool("IsForward", dir.y >= 0);
                }

                t += Time.deltaTime * moveSpeed;
                while (t >= 1f) {
                    t -= 1f;
                    idx++;
                    if (idx >= path.Count - 1) {
                        agent.transform.position = path[path.Count - 1];
                        if (animator != null) animator.SetBool("IsWalking", false);
                        yield break;
                    }
                }

                agent.transform.position = Vector3.Lerp(path[idx], path[idx + 1], t);
                yield return null;
            }

            agent.transform.position = path[path.Count - 1];
            if (animator != null) animator.SetBool("IsWalking", false);
        }

        /// <summary>Replay a peer ability use (network unit id).</summary>
        public void ApplyRemoteAbilityForUnit(string unitId, int abilityIndex, int mapX, int mapY) {
            if (string.IsNullOrEmpty(unitId) || gridMap == null) return;
            if (!TryGetAgentByNetworkUnitId(unitId, out var agent)) return;
            var abilities = agent.GetAbilities();
            if (abilityIndex < 0 || abilityIndex >= abilities.Count) return;
            var ability = abilities[abilityIndex];
            var map = gridMap.MapManager.ActiveMap;
            var tile = map.GetTileAtPosition(new Vector2Int(mapX, mapY));
            if (tile == null) return;
            var aux = new AbilityUseContext {
                Ability = ability,
                Caster = agent,
                TargetTile = tile,
                TurnNumber = currentTurn
            };
            agent.UseAbility(aux);
        }

        private void BuildNetworkUnitIdMap() {
            networkUnitIdsByAgent.Clear();
            agentsByNetworkUnitId.Clear();

            AssignTeamUnitIds(gridMap != null ? gridMap.RedAgents : null, "r");
            AssignTeamUnitIds(gridMap != null ? gridMap.BlueAgents : null, "b");
        }

        private void AssignTeamUnitIds(IReadOnlyList<Agent> teamAgents, string teamPrefix) {
            if (teamAgents == null) return;
            int index = 0;
            for (int i = 0; i < teamAgents.Count; i++) {
                var agent = teamAgents[i];
                if (agent == null) continue;

                string unitId = $"{teamPrefix}{index}";
                networkUnitIdsByAgent[agent] = unitId;
                agentsByNetworkUnitId[unitId] = agent;
                index++;
            }
        }

        /// <summary>
        /// Counts the number of Red team agents still alive in the turn order.
        /// </summary>
        private int CountRedAgents() {
            int redCount = 0;
            if (gridMap == null || gridMap.RedAgents == null) return 0;
            
            foreach (var agent in gridMap.RedAgents) {
                // Check if agent exists and is active (not destroyed)
                if (agent != null && agent.gameObject != null && agent.gameObject.activeInHierarchy && agent.HP > 0) {
                    redCount++;
                }
            }
            return redCount;
        }

        /// <summary>
        /// Counts the number of Blue team agents still alive in the turn order.
        /// </summary>
        private int CountBlueAgents() {
            int blueCount = 0;
            if (gridMap == null || gridMap.BlueAgents == null) return 0;
            
            foreach (var agent in gridMap.BlueAgents) {
                // Check if agent exists and is active (not destroyed)
                if (agent != null && agent.gameObject != null && agent.gameObject.activeInHierarchy && agent.HP > 0) {
                    blueCount++;
                }
            }
            return blueCount;
        }

        /// <summary>
        /// Checks if one team has been completely wiped out. If so, loads the victory scene.
        /// </summary>
        private void CheckForVictoryCondition() {
            int redAgents = CountRedAgents();
            int blueAgents = CountBlueAgents();

            if (redAgents == 0 && blueAgents > 0) {
                // Blue team wins
                Debug.Log("[BattleManager] Red team has been completely wiped out! Blue team wins!");
                LoadVictoryScene(TeamColor.Blue);
            } else if (blueAgents == 0 && redAgents > 0) {
                // Red team wins
                Debug.Log("[BattleManager] Blue team has been completely wiped out! Red team wins!");
                LoadVictoryScene(TeamColor.Red);
            }
        }

        /// <summary>
        /// Loads the victory scene with the winning team information.
        /// </summary>
        private void LoadVictoryScene(TeamColor winner) {
            // Store the winning team in PlayerPrefs so the victory scene can access it
            PlayerPrefs.SetString("WinningTeam", winner.ToString());
            PlayerPrefs.Save();

            // Update match stats with the winner
            if (match != null) {
                string winnerTeamId = winner == TeamColor.Red ? "Red" : "Blue";
                match.EndMatch(winnerTeamId);
                Debug.Log(match.matchStats.ToJson());
            }

            // Update player match stats
            foreach (Agent agent in turnOrder) {
                if (agent != null && agent.playerMatchStats != null && agent.playerMatchStats.playerId != 0) {
                    agent.RecordPlayerStats();
                    Debug.Log(agent.playerMatchStats.ToJson());
                } else {
                    Debug.LogWarning($"[BattleManager] Skipping stats for {agent?.name}");
                }
            }

            // Update matchup stats for 1 v 1
            if (currentMatchup != null && turnOrder.Count == 2) {
                string winnerName = null;

                foreach (Agent agent in turnOrder) {
                    if (agent != null && agent.HP > 0) {
                        winnerName = agent.Name;
                        break;
                    }
                }
                if (!string.IsNullOrEmpty(winnerName)) {
                    match.ResolveMatchup(winnerName);
                    Debug.Log($"[BattleManager] Matchup resolved. Winner: {winnerName}");
                } else {
                    Debug.LogWarning("[BattleManager] Could not determine matchup winner.");
                }
            }

            // Load the victory scene
            SceneManager.LoadScene("VictoryScene");
        }

        // ------------------------------------------------------------------ //
        // Tile click handling (runs before OnGUI each frame)
        // ------------------------------------------------------------------ //

        protected virtual void Update() {

            UpdateGUIRect();
            
            // Handle NPC behavior if current agent is an NPC
            if (currentNPCBehavior != null && currentNPCBehavior.IsNPC) {
                currentNPCBehavior.UpdateAI(gridMap, this);
            }
            
            // Handle turn timer (run in all player action states, including during movement animation)
            var obTimer = GetComponent<OnlineBattleManager>();
            bool useServerOnlyTimer = obTimer != null && obTimer.UsesServerTurnTimer;
            if (!useServerOnlyTimer && timerActive && (state == BattleState.WaitingForAction || state == BattleState.SelectingMoveTile || state == BattleState.SelectingAbility || state == BattleState.SelectingAbilityTarget || state == BattleState.MovingAgent)) {
                turnTimer -= Time.deltaTime;
                if (turnTimer <= 0f) {
                    turnTimer = 0f;
                    timerActive = false;
                    if (state == BattleState.MovingAgent) {
                        // Wait for movement animation to finish before advancing turn
                        state = BattleState.WaitingForAnimations;
                    } else {
                        AdvanceTurn();
                    }
                }
            }

            // Update movement tween
            if (state == BattleState.MovingAgent || state == BattleState.WaitingForAnimations) {
                UpdateMoveTween();
            }

            if (state == BattleState.SelectingMoveTile) {
                HandleMoveTileSelection();
            }
            else if (state == BattleState.SelectingAbility) {
                HandleAbilitySelection();
            }
            else if (state == BattleState.SelectingAbilityTarget) {
                UpdateAbilityTargetHoverPreview();
                HandleAbilityTargetSelection();
            }
        }

        private void UpdateAbilityTargetHoverPreview() {
            if (gridMap == null || state != BattleState.SelectingAbilityTarget || selectedAbility == null) {
                gridMap?.ClearPreviewHighlights();
                lastHoveredAbilityTargetTile = null;
                return;
            }

            Tile hovered = gridMap.GetHoveredTile();
            if (hovered == null || validAbilityTargets == null || !validAbilityTargets.Contains(hovered)) {
                if (lastHoveredAbilityTargetTile != null) {
                    gridMap.ClearPreviewHighlights();
                    lastHoveredAbilityTargetTile = null;
                }
                return;
            }

            if (hovered == lastHoveredAbilityTargetTile) return;

            lastHoveredAbilityTargetTile = hovered;
            gridMap.ClearPreviewHighlights();

            Agent caster = CurrentAgent;
            if (caster == null) return;

            var ctx = new AbilityUseContext {
                Ability = selectedAbility,
                Caster = caster,
                TargetTile = hovered,
                TurnNumber = currentTurn
            };

            var aoeTiles = Ability.GetTilesInShape(ctx);
            if (aoeTiles == null || aoeTiles.Count == 0) return;

            // Don't preview the target tile itself; the orange highlight already communicates selection.
            aoeTiles.RemoveAll(t => t == null || t == hovered);
            gridMap.HighlightTilesPreview(aoeTiles, abilityAoePreviewColor, abilityAoePreviewAlpha);
        }

        private void UpdateGUIRect() {
            // guiRect now takes up the full screen height, width is set by scaleHeight (interpreted as width percent)
            float width = Mathf.Clamp01(scaleWidth) * Screen.width;
            if (width < 200f) width = 200f; // minimum width for usability
            uiRect = new Rect(0, 0, width, Screen.height);
        }

        private void HandleMoveTileSelection() {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            if (!LocalPlayerMayControlTurn()) return;

            // Ignore clicks over the UI panel
            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector2 guiMouse = new Vector2(mouseScreen.x, Screen.height - mouseScreen.y);
            // Only when using the No longer needed IMGUI UI
            if (!useCanvasUI && uiRect.Contains(guiMouse)) return;

            Tile clickedTile = gridMap.GetHoveredTile();
            if (clickedTile == null || !validMoveTiles.Contains(clickedTile)) return;
            TryMoveCurrentAgentWithAnimation(clickedTile);
        }

        /// <summary>
        /// Executes move logic + tween for the current agent. Used by both player clicks and NPC AI.
        /// </summary>
        public bool TryMoveCurrentAgentWithAnimation(Tile destinationTile) {
            return TryMoveAgentWithAnimation(CurrentAgent, destinationTile, notifyAsLocalMove: true);
        }

        bool TryMoveAgentWithAnimation(Agent agent, Tile destinationTile, bool notifyAsLocalMove) {
            if (agent == null || destinationTile == null || gridMap == null || gridMap.MapManager == null || gridMap.MapManager.ActiveMap == null)
                return false;

            Vector2Int? oldPos = gridMap.MapManager.ActiveMap.GetCurrentTile(agent)?.Position;
            if (!oldPos.HasValue)
                return false;

            Vector2Int newPos = destinationTile.Position;
            var tilePath = gridMap.MapManager.ActiveMap.FindShortestPath(oldPos.Value, newPos);
            int pathLength = (tilePath != null && tilePath.Count > 0) ? tilePath.Count - 1 : 0;

            float agentZ = agent.transform.position.z;
            List<Vector3> worldPath = new List<Vector3>();
            if (tilePath != null) {
                foreach (var t in tilePath)
                    worldPath.Add(gridMap.MapIndexToWorldPosition(t.Position, agentZ));
            }

            if (!gridMap.TryMoveAgentByMapIndex(agent, destinationTile.Position))
                return false;

            if (pathLength > 0)
                agent.SpendMovement(pathLength);

            if (notifyAsLocalMove)
                OnAfterLocalMoveCommitted(destinationTile.Position);

            gridMap.ClearHighlights();
            validMoveTiles.Clear();

            // Start movement tween (snap back to old position first, then animate along path).
            if (worldPath.Count >= 2) {
                agent.transform.position = worldPath[0];
                movePath = worldPath;
                movePathIndex = 0;
                moveLerpT = 0f;
                movingAgent = agent;
                state = BattleState.MovingAgent;
            } else {
                state = BattleState.WaitingForAction;
            }
            return true;
        }

        private void HandleAbilitySelection() {
            // No-op: ability selection is handled by GUI.Button in DrawAbilitySelection
        }

        private void UpdateMoveTween() {
            if (movingAgent == null || movePath == null || movePath.Count < 2) {
                OnMoveAnimationComplete();
                return;
            }

            moveLerpT += Time.deltaTime * moveSpeed;
            Animator animator = movingAgent.GetComponentInChildren<Animator>();
            while (moveLerpT >= 1f) {
                moveLerpT -= 1f;
                movePathIndex++;
                if (movePathIndex >= movePath.Count - 1) {
                    // Arrived at destination
                    movingAgent.transform.position = movePath[movePath.Count - 1];
                    if (animator != null) {
                        animator.SetBool("IsWalking", false);
                    }
                    OnMoveAnimationComplete();
                    return;
                }
            }
            // Set animation bools
           
            if (animator != null) {
                animator.SetBool("IsWalking", true);
                Vector3 dir = (movePath[movePathIndex] - movePath[movePathIndex + 1]).normalized;
                if (dir.x < 0) {
                    animator.SetBool("IsLeft", false);
                } else {
                    animator.SetBool("IsLeft", true);
                }
                if (dir.y < 0) {
                    animator.SetBool("IsForward", false);
                } else {
                    animator.SetBool("IsForward", true);
                }
                //Debug.Log("IsLeft: " + animator.GetBool("IsLeft") + "\nIsForward: " + animator.GetBool("IsForward"));
            }
            movingAgent.transform.position = Vector3.Lerp(
                movePath[movePathIndex],
                movePath[movePathIndex + 1],
                Mathf.Clamp01(moveLerpT));
        }

        private void OnMoveAnimationComplete() {
            // Snap to final position just in case
            if (movingAgent != null && movePath != null && movePath.Count > 0)
                movingAgent.transform.position = movePath[movePath.Count - 1];

            movePath = null;
            movingAgent = null;
            movePathIndex = 0;
            moveLerpT = 0f;

            if (state == BattleState.WaitingForAnimations) {
                // Turn ended while moving - advance to next turn now
                AdvanceTurn();
            } else {
                // Movement finished within the turn - return to action menu
                state = BattleState.WaitingForAction;
            }
        }

        private void HandleAbilityTargetSelection() {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;
            if (!LocalPlayerMayControlTurn()) return;

            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector2 guiMouse = new Vector2(mouseScreen.x, Screen.height - mouseScreen.y);

            // Ignore clicks over the UI panel
            if (!useCanvasUI && uiRect.Contains(guiMouse)) return;

            Tile clickedTile = gridMap.GetHoveredTile();
            if (clickedTile == null || !validAbilityTargets.Contains(clickedTile)) return;

            OnAbilityTargetTileSelected(clickedTile);
        }

        // ------------------------------------------------------------------ //
        // Demo UI (IMGUI) - replace with Canvas buttons for production
        // ------------------------------------------------------------------ //

        void OnGUI() {
            if (useCanvasUI) return; // skip the IMGUI demo UI and use Canvas UI
            // Debug: Show mouse position in GUI coordinates and draw a red dot
            if (Mouse.current != null) {
                Vector2 mouseScreen = Mouse.current.position.ReadValue();
                Vector2 guiMouse = new Vector2(mouseScreen.x, Screen.height - mouseScreen.y);
                var mouseLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                mouseLabelStyle.normal.textColor = Color.red;
                GUI.Label(new Rect(10, Screen.height - 30, 300, 20), $"Mouse GUI: {guiMouse.x:F1}, {guiMouse.y:F1}", mouseLabelStyle);
                // Draw a small red dot at the mouse position
                Color prevColor = GUI.color;
                GUI.color = Color.red;
                GUI.DrawTexture(new Rect(guiMouse.x - 3, guiMouse.y - 3, 6, 6), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0);
                GUI.color = prevColor;
            }
            if (state == BattleState.NotStarted || CurrentAgent == null) return;
            // scale the box to the screen size (for better mobile visibility)
            GUI.Box(uiRect, "", GUI.skin.window);

            // Player Stats Box - anchored to top-right edge of uiRect
            DrawPlayerStats();

            var labelStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = Color.white;
            var labelRect = new Rect(15 + uiRect.xMin, 12 + uiRect.yMin, uiRect.width - 30, 30);
            
            // Show if this is an NPC's turn
            string turnLabel = CurrentAgent.Name + "'s Turn";
            if (currentNPCBehavior != null && currentNPCBehavior.IsNPC) {
                turnLabel += " (NPC)";
            }
            GUI.Label(labelRect, turnLabel, labelStyle);

            float btnW = 115, btnH = 35, btnY = 55 + uiRect.yMin;

            // Always show turn timer in top right during player's turn
            if (CurrentAgent != null && (timerActive || state == BattleState.MovingAgent || state == BattleState.WaitingForAnimations)) {
                float timerBoxW = 140, timerBoxH = 50;
                float timerBoxX = Screen.width - timerBoxW - 20;
                float timerBoxY = 20;
                Rect timerRect = new Rect(timerBoxX, timerBoxY, timerBoxW, timerBoxH);
                GUI.Box(timerRect, "");
                var timerStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                timerStyle.normal.textColor = Color.yellow;
                GUI.Label(new Rect(timerBoxX, timerBoxY + 8, timerBoxW, 30), $"Time: {turnTimer:F1}s", timerStyle);
            }

            // Only show action buttons if it's a player-controlled agent
            if (currentNPCBehavior == null || !currentNPCBehavior.IsNPC) {
                if (state == BattleState.WaitingForAction) {
                    float actionBtnW = uiRect.width - 30;
                    float actionBtnH = 40;
                    float actionBtnX = uiRect.xMin + 15;
                    float actionBtnY = uiRect.yMin + 60;

                    // Check if current agent has movement left
                    bool canMove = false;
                    if (CurrentAgent != null && gridMap != null && gridMap.MapManager != null && gridMap.MapManager.ActiveMap != null) {
                        var movableTiles = gridMap.MapManager.ActiveMap.GetMovableTiles(CurrentAgent);
                        canMove = movableTiles != null && movableTiles.Count > 0;
                    }

                    // Grey out and disable Move button if no movement left
                    GUI.enabled = canMove;
                    if (GUI.Button(new Rect(actionBtnX, actionBtnY, actionBtnW, actionBtnH), "Move") && canMove)
                        OnMovePressed();
                    GUI.enabled = true;

                    if (GUI.Button(new Rect(actionBtnX, actionBtnY + actionBtnH + 10, actionBtnW, actionBtnH), "Use Ability"))
                        OnUseAbilityPressed();
                    if (GUI.Button(new Rect(actionBtnX, actionBtnY + 2 * (actionBtnH + 10), actionBtnW, actionBtnH), "Pass Turn"))
                        OnEndTurnPressed();
                }
                else if (state == BattleState.SelectingMoveTile) {
                    if (GUI.Button(new Rect(15, btnY, btnW, btnH), "Cancel"))
                        OnCancelPressed();

                    var hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                    hintStyle.normal.textColor = Color.yellow;
                    GUI.Label(new Rect(15, btnY + btnH + 4, 240, 20),
                        "Click a highlighted tile to move", hintStyle);
                }
                else if (state == BattleState.SelectingAbility) {
                    DrawAbilitySelection();
                }
                else if (state == BattleState.SelectingAbilityTarget && selectedAbility != null) {
                    DrawAbilityTargeting();
                }
            }
            
            // Show movement indicator for everyone
            if (state == BattleState.MovingAgent || state == BattleState.WaitingForAnimations) {
                var movingStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
                movingStyle.normal.textColor = Color.yellow;
                GUI.Label(new Rect(15 + uiRect.xMin, 55 + uiRect.yMin, uiRect.width - 30, 30), "Moving...", movingStyle);
            }
        }

        void DrawPlayerStats() {
            float statsW = 220f;
            float statsH = 110f;
            float statsX = uiRect.xMax;
            float statsY = 0f;
            Rect statsRect = new Rect(statsX, statsY, statsW, statsH);
            GUI.Box(statsRect, $"{CurrentAgent.Name}'s Stats");
            var stats = CurrentAgent.playerMatchStats;
            float labelX = statsX + 10;
            float labelStartY = statsY + 25;
            if (stats != null) {
                GUI.Label(new Rect(labelX, labelStartY, statsW - 20, 20), $"Damage Dealt: {stats.damageDealt}");
                GUI.Label(new Rect(labelX, labelStartY + 20, statsW - 20, 20), $"Damage Taken: {stats.damageTaken}");
                GUI.Label(new Rect(labelX, labelStartY + 40, statsW - 20, 20), $"Turns Taken: {stats.turnsTaken}");
                GUI.Label(new Rect(labelX, labelStartY + 60, statsW - 20, 20), $"HP: {CurrentAgent.HP}");
            }
        }

        void DrawAbilityTargeting() {
            var headerStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            headerStyle.normal.textColor = Color.cyan;
            var targetingRect = new Rect(10 + uiRect.xMin, 50 + uiRect.yMin, uiRect.width - 20, 25);
            GUI.Label(targetingRect, $"Target for {selectedAbility.DisplayName}", headerStyle);

            var hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            hintStyle.normal.textColor = Color.yellow;
            GUI.Label(new Rect(10 + uiRect.xMin, 80 + uiRect.yMin, uiRect.width - 20, 40),
                "Click an orange highlighted tile to target", hintStyle);

            float btnY = 130, btnW = 150, btnH = 35;
            if (GUI.Button(new Rect(25 + uiRect.xMin, btnY + uiRect.yMin, btnW, btnH), "Cancel Target")) {
                OnAbilityTargetCancelled();
            }
        }

        void DrawAbilitySelection() {
            float headerPadX = 20f;
            float headerPadY = 40f;
            float headerHeight = 20f;
            if (availableAbilities.Count == 0) return;

            var headerStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            headerStyle.normal.textColor = Color.cyan;
            // Span the full screen width, right-aligned, with extra padding from the top
            GUI.Label(new Rect(headerPadX, uiRect.yMin + headerPadY, uiRect.width - 2 * headerPadX, headerHeight), "Abilities (Click to select)", headerStyle);

            // Layout: abilities stacked vertically in a single column
            float padding = 10f;
            float navBtnH = 30f;
            float navBtnW = (uiRect.width - 3 * padding) / 2f;
            float confirmBtnH = 30f;
            float confirmBtnY = uiRect.yMin + uiRect.height - confirmBtnH - padding;
            float navBtnY = confirmBtnY - navBtnH - padding;
            float abilityAreaY = uiRect.yMin + headerPadY + headerHeight + padding;
            int abilityCount = availableAbilities.Count;
            if (abilityCount == 0) return;
            float availableH = navBtnY - abilityAreaY; // No extraBtnSpace subtraction
            float abilityH = (availableH - (abilityCount - 1) * padding) / abilityCount;
            float abilityW = uiRect.width - 2 * padding;
            for (int i = 0; i < abilityCount; i++) {
                float x = uiRect.xMin + padding;
                float y = abilityAreaY + i * (abilityH + padding);
                if (y + abilityH > navBtnY) break;
                Rect abilityRect = new Rect(x, y, abilityW, abilityH);
                Ability ability = availableAbilities[i];
                bool isSelected = i == selectedAbilityIndex;
                // Draw ability as a button for perfect hitbox alignment
                Color prevColor = GUI.color;
                if (isSelected) {
                    GUI.color = new Color(1f, 1f, 0.5f, 0.3f);
                    GUI.Box(abilityRect, "");
                }
                GUI.color = prevColor;
                if (GUI.Button(abilityRect, GUIContent.none, GUIStyle.none)) {
                    selectedAbilityIndex = i;
                    Debug.Log($"BattleManager: Selected ability {selectedAbilityIndex}: {ability.DisplayName}");
                }
                // Draw ability name and info inside the button
                var abilityNameStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true,
                    alignment = TextAnchor.UpperCenter
                };
                abilityNameStyle.normal.textColor = isSelected ? Color.yellow : Color.white;
                string cooldownText = !CurrentAgent.CanUseAbility(ability) ? " (Cooldown)" : "";
                GUI.Label(new Rect(x + 5, y + 5, abilityW - 10, 20), ability.DisplayName + cooldownText, abilityNameStyle);
                // Ability info (target type, range, cost)
                var infoStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 11,
                    wordWrap = true,
                    alignment = TextAnchor.UpperCenter
                };
                infoStyle.normal.textColor = Color.gray;
                string infoText = $"Range: {ability.RangeMin}-{ability.RangeMax} | Cost: {ability.Cost} | Target: {ability.TargetType}";
                GUI.Label(new Rect(x + 5, y + 30, abilityW - 10, abilityH - 35), infoText, infoStyle);
            }

            // Navigation buttons (Prev/Next side-by-side)
            float navBtnX = uiRect.xMin + padding;
            if (GUI.Button(new Rect(navBtnX, navBtnY, navBtnW, navBtnH), "< Prev")) {
                OnPreviousAbilityPressed();
            }
            if (GUI.Button(new Rect(navBtnX + navBtnW + padding, navBtnY, navBtnW, navBtnH), "Next >")) {
                OnNextAbilityPressed();
            }
            // Confirm/Cancel buttons side-by-side below
            float confirmBtnX = uiRect.xMin + padding;
            if (GUI.Button(new Rect(confirmBtnX, confirmBtnY, navBtnW, confirmBtnH), "Confirm")) {
                OnConfirmAbilityPressed();
            }
            if (GUI.Button(new Rect(confirmBtnX + navBtnW + padding, confirmBtnY, navBtnW, confirmBtnH), "Cancel")) {
                OnAbilityCancelled();
            }

            // Info text: place just above navigation buttons, even if it overlaps the last ability box
            var infoTextStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            infoTextStyle.normal.textColor = Color.yellow;
            float infoLabelY = navBtnY - 20; // 20px above nav buttons
            GUI.Label(new Rect(confirmBtnX, infoLabelY, uiRect.width - 2 * padding, 20),
                $"Ability {selectedAbilityIndex + 1} of {availableAbilities.Count}", infoTextStyle);
        }
    }
}