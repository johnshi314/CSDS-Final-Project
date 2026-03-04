using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using NetFlower.UI;

namespace NetFlower {

    public enum BattleState {
        NotStarted,
        WaitingForAction,
        SelectingMoveTile,
        SelectingAbility,
        SelectingAbilityTarget
    }

    /// <summary>
    /// Drives a turn-based battle demo. Owns the state machine that decides
    /// what mouse clicks do, cycles turns between agents, and draws IMGUI
    /// buttons for Move / End Turn / Cancel.
    /// </summary>
    public class BattleManager : MonoBehaviour {

    // Turn timer
    private float turnTimer = 30f;
    private const float TURN_TIME_LIMIT = 30f;
    private bool timerActive = false;

        [Header("Turn Management")]
        public int currentTurn = 0; // Tracks the current turn number (starting from 0)

        [Header("References")]
        [SerializeField] private GridMap gridMap;

        [Header("Tile Highlighting")]
        [SerializeField] private Color moveRangeColor = new Color(0.2f, 0.6f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float moveRangeAlpha = 0.5f;
        [SerializeField] private Color abilityTargetColor = new Color(1f, 0.5f, 0.2f, 1f);
        [SerializeField, Range(0f, 1f)] private float abilityTargetAlpha = 0.5f;

        // State
        private BattleState state = BattleState.NotStarted;
        private List<Agent> turnOrder = new List<Agent>();
        private int currentAgentIndex;
        private List<Tile> validMoveTiles = new List<Tile>();
        
        // Ability selection state
        private List<Ability> availableAbilities = new List<Ability>();
        private int selectedAbilityIndex = 0;
        private Ability selectedAbility = null;  // The ability currently being targeted
        private List<Tile> validAbilityTargets = new List<Tile>();  // Valid target tiles for the selected ability
        
        // GUI rect used to block tile clicks over the UI panel
        private readonly Rect guiRect = new Rect(5, 5, 350, 400);

        public BattleState State => state;
        public Agent CurrentAgent =>
            (turnOrder.Count > 0 && currentAgentIndex < turnOrder.Count)
                ? turnOrder[currentAgentIndex]
                : null;

        // ------------------------------------------------------------------ //
        // Public API (called by GameplayDemo or wired to Canvas buttons)
        // ------------------------------------------------------------------ //

        public void StartBattle() {
            if (gridMap == null) {
                gridMap = GetComponent<GridMap>();
                if (gridMap == null)
                    gridMap = GetComponentInParent<GridMap>();
            }

            if (gridMap == null || gridMap.MapManager == null) {
                Debug.LogError("BattleManager: GridMap or MapManager not available.");
                return;
            }

            // Interleave red/blue so turns alternate between teams
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

            currentAgentIndex = 0;
            Debug.Log($"BattleManager: Battle started with {turnOrder.Count} agents.");
            BeginTurn();
        }

        public void OnMovePressed() {
            if (state != BattleState.WaitingForAction) return;

            Agent agent = CurrentAgent;
            if (agent == null) return;

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

            Agent agent = CurrentAgent;
            var aux = new AbilityUseContext {
                Ability = selectedAbility,
                Caster = agent,
                TargetTile = targetTile,
                TurnNumber = currentTurn
            };
            agent.UseAbility(aux);
            Debug.Log($"BattleManager: {agent.Name} used {selectedAbility.DisplayName} on tile {targetTile.Position}.");

            gridMap.ClearHighlights();
            validAbilityTargets.Clear();
            state = BattleState.WaitingForAction;
            availableAbilities.Clear();
            selectedAbility = null;
            selectedAbilityIndex = 0;
            // Do not advance turn automatically after using ability
        }

        public void OnAbilityTargetCancelled() {
            if (state != BattleState.SelectingAbilityTarget) return;

            gridMap.ClearHighlights();
            validAbilityTargets.Clear();
            state = BattleState.SelectingAbility;
            selectedAbility = null;
            Debug.Log("BattleManager: Ability targeting cancelled.");
        }

        public void OnEndTurnPressed() {
            if (state == BattleState.NotStarted) return;

            gridMap.ClearHighlights();
            validMoveTiles.Clear();
            AdvanceTurn();
        }

        // ------------------------------------------------------------------ //
        // Internal turn flow
        // ------------------------------------------------------------------ //

        private void BeginTurn() {
            state = BattleState.WaitingForAction;
            gridMap.ClearHighlights();
            validMoveTiles.Clear();
            // Tick all agents' effects at the start of each turn
            foreach (var agent in turnOrder)
            {
                agent.TickEffects(currentTurn);
            }
            if (CurrentAgent != null)
                CurrentAgent.OnTurnStart();
            Debug.Log($"BattleManager: {CurrentAgent.Name}'s turn. (Turn {currentTurn + 1})");

            // Start turn timer
            turnTimer = TURN_TIME_LIMIT;
            timerActive = true;
        }

        private void AdvanceTurn() {
            if (CurrentAgent != null)
                CurrentAgent.OnTurnEnd();
            // Increment turn number when looping back to the first agent
            currentAgentIndex = (currentAgentIndex + 1) % turnOrder.Count;
            if (currentAgentIndex == 0)
                currentTurn++;
            BeginTurn();
            // Do not stop timer here; BeginTurn restarts it for the next player
        }

        // ------------------------------------------------------------------ //
        // Tile click handling (runs before OnGUI each frame)
        // ------------------------------------------------------------------ //

        void Update() {
            // Handle turn timer (run in all player action states)
            if (timerActive && (state == BattleState.WaitingForAction || state == BattleState.SelectingMoveTile || state == BattleState.SelectingAbility || state == BattleState.SelectingAbilityTarget)) {
                turnTimer -= Time.deltaTime;
                if (turnTimer <= 0f) {
                    turnTimer = 0f;
                    timerActive = false;
                    AdvanceTurn();
                }
            }
            if (state == BattleState.SelectingMoveTile) {
                HandleMoveTileSelection();
            }
            else if (state == BattleState.SelectingAbility) {
                HandleAbilitySelection();
            }
            else if (state == BattleState.SelectingAbilityTarget) {
                HandleAbilityTargetSelection();
            }
        }

        private void HandleMoveTileSelection() {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            // Ignore clicks over the UI panel
            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector2 guiMouse = new Vector2(mouseScreen.x, Screen.height - mouseScreen.y);
            if (guiRect.Contains(guiMouse)) return;

            Tile clickedTile = gridMap.GetHoveredTile();
            if (clickedTile == null || !validMoveTiles.Contains(clickedTile)) return;

            Agent agent = CurrentAgent;
            // Calculate path distance moved using BFS
            Vector2Int? oldPos = gridMap.MapManager.ActiveMap.GetCurrentTile(agent)?.Position;
            Vector2Int newPos = clickedTile.Position;
            int pathLength = 0;
            if (oldPos.HasValue) {
                var path = gridMap.MapManager.ActiveMap.FindShortestPath(oldPos.Value, newPos);
                // Path includes start tile, so movement cost is path.Count - 1
                pathLength = (path != null && path.Count > 0) ? path.Count - 1 : 0;
            }
            gridMap.TryMoveAgentByMapIndex(agent, clickedTile.Position);
            // Decrease agent's movement range by path length moved
            if (pathLength > 0 && agent != null) {
                agent.Move(pathLength);
            }
            gridMap.ClearHighlights();
            validMoveTiles.Clear();
            // Return to main action menu so player can act again
            state = BattleState.WaitingForAction;
        }

        private void HandleAbilitySelection() {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector2 guiMouse = new Vector2(mouseScreen.x, Screen.height - mouseScreen.y);

            // Check if click is on an ability
            int clickedAbilityIndex = GetAbilityIndexAtMousePosition(guiMouse);
            if (clickedAbilityIndex >= 0) {
                selectedAbilityIndex = clickedAbilityIndex;
                Debug.Log($"BattleManager: Selected ability {selectedAbilityIndex}: {availableAbilities[selectedAbilityIndex].DisplayName}");
            }
        }

        private void HandleAbilityTargetSelection() {
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector2 guiMouse = new Vector2(mouseScreen.x, Screen.height - mouseScreen.y);

            // Ignore clicks over the UI panel
            if (guiRect.Contains(guiMouse)) return;

            Tile clickedTile = gridMap.GetHoveredTile();
            if (clickedTile == null || !validAbilityTargets.Contains(clickedTile)) return;

            OnAbilityTargetTileSelected(clickedTile);
        }

        private int GetAbilityIndexAtMousePosition(Vector2 guiMouse) {
            if (availableAbilities.Count == 0) return -1;

            float startY = 80;
            float abilitySpacing = 75;
            float abilityHeight = 70;
            float abilityWidth = 310;

            int displayCount = Mathf.Min(4, availableAbilities.Count);
            for (int i = 0; i < displayCount; i++) {
                float posY = startY + (i * abilitySpacing);
                Rect abilityRect = new Rect(20, posY - 5, abilityWidth, abilityHeight);

                if (abilityRect.Contains(guiMouse)) {
                    return i;
                }
            }

            return -1;
        }

        // ------------------------------------------------------------------ //
        // Demo UI (IMGUI) — replace with Canvas buttons for production
        // ------------------------------------------------------------------ //

        void OnGUI() {
            if (state == BattleState.NotStarted || CurrentAgent == null) return;

            GUI.Box(guiRect, "");

            var labelStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            labelStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(15, 12, 240, 30), $"{CurrentAgent.Name}'s Turn", labelStyle);

            float btnW = 115, btnH = 35, btnY = 55;

            // Always show turn timer in top right during player's turn
            if (CurrentAgent != null && timerActive) {
                float timerBoxW = 140, timerBoxH = 50;
                float timerBoxX = Screen.width - timerBoxW - 20;
                float timerBoxY = 20;
                Rect timerRect = new Rect(timerBoxX, timerBoxY, timerBoxW, timerBoxH);
                GUI.Box(timerRect, "");
                var timerStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                timerStyle.normal.textColor = Color.yellow;
                GUI.Label(new Rect(timerBoxX, timerBoxY + 8, timerBoxW, 30), $"Time: {turnTimer:F1}s", timerStyle);
            }

            if (state == BattleState.WaitingForAction) {
                if (GUI.Button(new Rect(15, btnY, btnW, btnH), "Move"))
                    OnMovePressed();

                if (GUI.Button(new Rect(15 + btnW + 10, btnY, btnW, btnH), "Use Ability"))
                    OnUseAbilityPressed();

                if (GUI.Button(new Rect(15, btnY + btnH + 5, btnW * 2 + 10, btnH), "Pass Turn"))
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

        void DrawAbilityTargeting() {
            var headerStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            headerStyle.normal.textColor = Color.cyan;
            GUI.Label(new Rect(25, 50, 320, 25), $"Target for {selectedAbility.DisplayName}", headerStyle);

            var hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            hintStyle.normal.textColor = Color.yellow;
            GUI.Label(new Rect(25, 80, 320, 40),
                "Click an orange highlighted tile to target", hintStyle);

            float btnY = 130, btnW = 150, btnH = 35;
            if (GUI.Button(new Rect(25, btnY, btnW, btnH), "Cancel Target")) {
                OnAbilityTargetCancelled();
            }
        }

        void DrawAbilitySelection() {
            if (availableAbilities.Count == 0) return;

            var headerStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            headerStyle.normal.textColor = Color.cyan;
            GUI.Label(new Rect(25, 50, 320, 25), "Abilities (Click to select)", headerStyle);

            float startY = 80;
            float abilityHeight = 70;
            float abilitySpacing = 75;

            // Draw all abilities (showing up to 4 at a time)
            int displayCount = Mathf.Min(4, availableAbilities.Count);
            for (int i = 0; i < displayCount; i++) {
                Ability ability = availableAbilities[i];
                bool isSelected = i == selectedAbilityIndex;
                float posY = startY + (i * abilitySpacing);

                // Background highlight for selected ability
                if (isSelected) {
                    GUI.Box(new Rect(20, posY - 5, 310, abilityHeight), "");
                    // Draw border around selected ability
                    GUI.skin.box.border.left = 2;
                    GUI.skin.box.border.right = 2;
                    GUI.skin.box.border.top = 2;
                    GUI.skin.box.border.bottom = 2;
                }

                // Ability name
                var abilityNameStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true
                };
                abilityNameStyle.normal.textColor = isSelected ? Color.yellow : Color.white;

                string cooldownText = !CurrentAgent.CanUseAbility(ability) ? " (Cooldown)" : "";
                GUI.Label(new Rect(30, posY, 290, 20), ability.DisplayName + cooldownText, abilityNameStyle);

                // Ability info (target type, range, cost)
                var infoStyle = new GUIStyle(GUI.skin.label) {
                    fontSize = 11,
                    wordWrap = true
                };
                infoStyle.normal.textColor = Color.gray;

                string infoText = $"Range: {ability.RangeMin}-{ability.RangeMax} | Cost: {ability.Cost} | Target: {ability.TargetType}";
                GUI.Label(new Rect(30, posY + 22, 290, 40), infoText, infoStyle);
            }

            // Navigation buttons
            float navBtnY = startY + (displayCount * abilitySpacing) + 10;
            float navBtnW = 75, navBtnH = 30;

            if (GUI.Button(new Rect(25, navBtnY, navBtnW, navBtnH), "< Prev")) {
                OnPreviousAbilityPressed();
            }

            if (GUI.Button(new Rect(105, navBtnY, navBtnW, navBtnH), "Next >")) {
                OnNextAbilityPressed();
            }

            if (GUI.Button(new Rect(185, navBtnY, navBtnW, navBtnH), "Confirm")) {
                OnConfirmAbilityPressed();
            }

            if (GUI.Button(new Rect(265, navBtnY, navBtnW, navBtnH), "Cancel")) {
                OnAbilityCancelled();
            }

            // Info text
            var infoTextStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            infoTextStyle.normal.textColor = Color.yellow;
            GUI.Label(new Rect(25, navBtnY + navBtnH + 5, 320, 30),
                $"Ability {selectedAbilityIndex + 1} of {availableAbilities.Count}", infoTextStyle);
        }
    }
}
