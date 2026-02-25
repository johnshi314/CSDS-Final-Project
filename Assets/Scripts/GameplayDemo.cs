using UnityEngine;
using NetFlower.UI;

namespace NetFlower {
    /// <summary>
    /// Bootstraps the battle demo. Waits for GridMap to finish initializing,
    /// then tells BattleManager to start.
    /// </summary>
    public class GameplayDemo : MonoBehaviour {
        [SerializeField] private GridMap gridMap;
        [SerializeField] private BattleManager battleManager;

        private bool battleStarted;

        void Start() {
            if (gridMap == null)
                gridMap = GetComponent<GridMap>();
            if (battleManager == null)
                battleManager = GetComponent<BattleManager>();

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
            battleManager.StartBattle();
        }
    }
}
