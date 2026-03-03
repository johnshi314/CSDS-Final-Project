using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using NetFlower.UI;

namespace NetFlower {

    public enum BattleState {
        NotStarted,
        WaitingForAction,
        SelectingMoveTile,
        SelectingAbility
    }

    /// <summary>
    /// Drives a turn-based battle demo. Owns the state machine that decides
    /// what mouse clicks do, cycles turns between agents, and draws IMGUI
    /// buttons for Move / End Turn / Cancel.
    /// </summary>
    public class BattleManager : MonoBehaviour {

        [Header("References")]
        [SerializeField] private GridMap gridMap;

        [Header("Movement Highlight")]
        [SerializeField] private Color moveRangeColor = new Color(0.2f, 0.6f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float moveRangeAlpha = 0.5f;

        // State
        private BattleState state = BattleState.NotStarted;
        private List<Agent> turnOrder = new List<Agent>();
        private int currentAgentIndex;
        private List<Tile> validMoveTiles = new List<Tile>();
        
        // Ability selection state
        private List<Ability> availableAbilities = new List<Ability>();
        private int selectedAbilityIndex = 0;

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
            Ability selectedAbility = availableAbilities[selectedAbilityIndex];

            // Check if ability can be used
            if (!agent.CanUseAbility(selectedAbility)) {
                Debug.LogWarning($"BattleManager: {agent.Name} cannot use {selectedAbility.DisplayName} (on cooldown or unavailable).");
                return;
            }

            // For now, use the ability on the current agent's tile (global or self-targeting)
            // TODO: Implement targeting system
            Tile currentTile = gridMap.MapManager.ActiveMap.GetCurrentTile(agent);
            if (currentTile != null) {
                agent.UseAbility(selectedAbility, currentTile);
                Debug.Log($"BattleManager: {agent.Name} used {selectedAbility.DisplayName}.");
            }

            state = BattleState.WaitingForAction;
            availableAbilities.Clear();
            selectedAbilityIndex = 0;
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
            Debug.Log($"BattleManager: {CurrentAgent.Name}'s turn.");
        }

        private void AdvanceTurn() {
            currentAgentIndex = (currentAgentIndex + 1) % turnOrder.Count;
            BeginTurn();
        }

        // ------------------------------------------------------------------ //
        // Tile click handling (runs before OnGUI each frame)
        // ------------------------------------------------------------------ //

        void Update() {
            if (state != BattleState.SelectingMoveTile) return;
            if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame) return;

            // Ignore clicks over the UI panel
            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector2 guiMouse = new Vector2(mouseScreen.x, Screen.height - mouseScreen.y);
            if (guiRect.Contains(guiMouse)) return;

            Tile clickedTile = gridMap.GetHoveredTile();
            if (clickedTile == null || !validMoveTiles.Contains(clickedTile)) return;

            Agent agent = CurrentAgent;
            gridMap.TryMoveAgentByMapIndex(agent, clickedTile.Position);
            gridMap.ClearHighlights();
            validMoveTiles.Clear();
            Debug.Log($"BattleManager: {agent.Name} moved to {clickedTile.Position}.");

            AdvanceTurn();
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

            if (state == BattleState.WaitingForAction) {
                if (GUI.Button(new Rect(15, btnY, btnW, btnH), "Move"))
                    OnMovePressed();

                if (GUI.Button(new Rect(15 + btnW + 10, btnY, btnW, btnH), "Use Ability"))
                    OnUseAbilityPressed();

                if (GUI.Button(new Rect(15, btnY + btnH + 5, btnW * 2 + 10, btnH), "End Turn"))
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
        }

        void DrawAbilitySelection() {
            if (availableAbilities.Count == 0) return;

            var headerStyle = new GUIStyle(GUI.skin.label) {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            headerStyle.normal.textColor = Color.cyan;
            GUI.Label(new Rect(25, 50, 320, 25), "Abilities", headerStyle);

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
