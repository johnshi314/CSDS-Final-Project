using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using NetFlower.UI;

namespace NetFlower {

    public enum BattleState {
        NotStarted,
        WaitingForAction,
        SelectingMoveTile
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

        // GUI rect used to block tile clicks over the UI panel
        private readonly Rect guiRect = new Rect(5, 5, 260, 110);

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

                if (GUI.Button(new Rect(15 + btnW + 10, btnY, btnW, btnH), "End Turn"))
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
        }
    }
}
