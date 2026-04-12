using UnityEngine;
using NetFlower.UI;

namespace NetFlower {
    /// <summary>
    /// Bootstraps the battle demo. Waits for GridMap to finish initializing,
    /// then tells BattleManager to start.
    /// When <see cref="PersistentPlayerPreferences.isPlayingOnline"/> is true, replaces <see cref="BattleManager"/>
    /// with <see cref="OnlineBattleManager"/> (same settings) so the battle uses the server WebSocket.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class GameplayDemo : MonoBehaviour {
        [SerializeField] private GridMap gridMap;
        [SerializeField] private BattleManager battleManager;

        private bool battleStarted;

        void Awake() {
            ResolveBattleManagerReference();

            var prefs = PersistentPlayerPreferences.instance;
            bool wantOnline = prefs != null && prefs.isPlayingOnline;

            // Prefab may have OnlineBattleManager; offline must use plain BattleManager.
            if (!wantOnline && battleManager is OnlineBattleManager omOff) {
                battleManager = BattleRuntime.ReplaceWithPlainBattleManager(omOff);
                Debug.Log("[GameplayDemo] Offline mode: using BattleManager (replaced OnlineBattleManager).");
                return;
            }

            if (!wantOnline)
                return;

            if (battleManager != null && battleManager is not OnlineBattleManager) {
                battleManager = BattleRuntime.ReplaceWithOnlineBattleManagerIfNeeded(battleManager);
                Debug.Log("[GameplayDemo] Online mode: using OnlineBattleManager (from PersistentPlayerPreferences).");
            }
        }

        void ResolveBattleManagerReference() {
            if (battleManager == null)
                battleManager = GetComponent<BattleManager>();
            if (battleManager == null)
                battleManager = GetComponentInParent<BattleManager>();
            if (battleManager == null)
                battleManager = GetComponentInChildren<BattleManager>(true);
        }

        void Start() {
            if (gridMap == null)
                gridMap = GetComponent<GridMap>();
            ResolveBattleManagerReference();

            if (gridMap == null) {
                Debug.LogError("GameplayDemo: GridMap not found!");
                return;
            }
            if (battleManager == null) {
                Debug.LogError("GameplayDemo: BattleManager not found!");
                return;
            }
        }

        void Update() {
            if (battleStarted) return;
            if (gridMap.MapManager == null) return;

            battleStarted = true;
            // Networked battle waits for server newTurn; local/offline or fallback runs BeginTurn immediately.
            if (battleManager is OnlineBattleManager online && online.UsesNetworkBattle)
                online.StartBattle(deferFirstBeginTurn: true);
            else
                battleManager.StartBattle();
        }
    }
}
