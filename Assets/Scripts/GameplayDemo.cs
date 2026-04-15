using UnityEngine;
using NetFlower.UI;
using System.Collections.Generic;

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
        [SerializeField] private AllAgents allAgents;
        [Header("Optional team roots in GameplayTest scene")]
        [SerializeField] private Transform redTeamRoot;
        [SerializeField] private Transform blueTeamRoot;

        private PersistentPlayerPreferences prefs;
        private bool battleStarted;
        private bool teamsInitialized;

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

            if (prefs == null) {
                prefs = PersistentPlayerPreferences.instance;
            }

            if (allAgents == null) {
                allAgents = Resources.Load<AllAgents>("Settings/AllAgents");
            }

            if (redTeamRoot == null)
                redTeamRoot = FindTeamRoot("RedTeam");
            if (blueTeamRoot == null)
                blueTeamRoot = FindTeamRoot("BlueTeam");
        }

        void Update() {
            if (battleStarted) return;
            if (gridMap.MapManager == null) return;

            // GridMap.Start initializes MapManager. Only after that should we reinitialize teams/spawns.
            if (!teamsInitialized) {
                if (prefs == null) {
                    prefs = PersistentPlayerPreferences.instance;
                    // No persistent prefs usually means GameplayTest was launched directly for quick debugging.
                    if (prefs == null) {
                        Debug.Log("GameplayDemo: No PersistentPlayerPreferences found; using scene default teams.");
                        teamsInitialized = true;
                        return;
                    }
                }

                if (prefs.isPlayingOnline) {
                    OnlineFillGridMap(prefs);
                } else {
                    OfflineFillGridMap();
                }

                teamsInitialized = true;
                return;
            }

            battleStarted = true;
            // Networked battle waits for server newTurn; local/offline or fallback runs BeginTurn immediately.
            if (battleManager is OnlineBattleManager online && online.UsesNetworkBattle)
                online.StartBattle(deferFirstBeginTurn: true);
            else
                battleManager.StartBattle();
        }

        /// <summary>
        /// Online PvP: same structural guarantees as <see cref="OfflineFillGridMap"/> - destroy scene default
        /// agents, spawn prefabs under RedTeam/BlueTeam so each client builds the same network unit ids (r0, b0).
        /// Local character comes from character select; other team uses a
        /// shared placeholder prefab until the server sends opponent roster (harpy mirroring offline for now).
        /// </summary>
        void OnlineFillGridMap(PersistentPlayerPreferences prefs) {
            if (allAgents == null) {
                allAgents = Resources.Load<AllAgents>("Settings/AllAgents");
            }

            GameObject localPrefab = allAgents != null ? allAgents.GetAgentPrefabById(prefs.characterId) : null;
            GameObject opponentPrefab = allAgents != null ? allAgents.harpyAgent : null;

            if (localPrefab == null) {
                Debug.LogError($"GameplayDemo (online): Selected player prefab is null. characterId={prefs?.characterId} (AllAgents harpyId={allAgents?.harpyId}, elfId={allAgents?.elfId})");
                return;
            }
            if (opponentPrefab == null) {
                Debug.LogError("GameplayDemo (online): Opponent placeholder prefab (harpyAgent) is null in AllAgents.");
                return;
            }

            var match = Match.GetInstance();
            bool localOnRed = match == null || match.selectedTeam == Match.TeamSelection.Red;

            DestroyDefaultTeamAgents();

            GameObject redObject;
            GameObject blueObject;
            if (localOnRed) {
                redObject = Instantiate(localPrefab, redTeamRoot);
                blueObject = Instantiate(opponentPrefab, blueTeamRoot);
            } else {
                redObject = Instantiate(opponentPrefab, redTeamRoot);
                blueObject = Instantiate(localPrefab, blueTeamRoot);
            }

            StripNpcBehaviorForOnlinePvP(redObject);
            StripNpcBehaviorForOnlinePvP(blueObject);

            Agent redAgent = redObject.GetComponent<Agent>();
            Agent blueAgent = blueObject.GetComponent<Agent>();
            if (redAgent == null || blueAgent == null) {
                Debug.LogError("GameplayDemo (online): Agent prefab is missing Agent component.");
                return;
            }

            var redTeam = new List<Agent> { redAgent };
            var blueTeam = new List<Agent> { blueAgent };
            gridMap.ReinitializeMapManager(redTeam, blueTeam, gridMap.RedSpawnPoints, gridMap.BlueSpawnPoints);
        }

        static void StripNpcBehaviorForOnlinePvP(GameObject agentObject) {
            if (agentObject == null) return;
            var npc = agentObject.GetComponent<NPCBehavior>();
            if (npc != null)
                Destroy(npc);
        }

        void OfflineFillGridMap() {

            // New list of red and blue teams
            List<Agent> redTeam = new List<Agent>();
            List<Agent> blueTeam = new List<Agent>();
            // Player agent comes from selection; default offline NPC is Harpy.
            GameObject playerPrefab = allAgents.GetAgentPrefabById(prefs.characterId);
            GameObject npcPrefab = allAgents.harpyAgent;
            if (playerPrefab == null) {
                Debug.LogError($"GameplayDemo: Selected player prefab is null. characterId={prefs?.characterId} (AllAgents harpyId={allAgents?.harpyId}, elfId={allAgents?.elfId})");
                return;
            }
            if (npcPrefab == null) {
                Debug.LogError("GameplayDemo: Harpy agent prefab is null in AllAgents.");
                return;
            }
            // Coming from CharacterSelect: remove default scene agents so only dynamic teams remain.
            DestroyDefaultTeamAgents();
            // Instantiate one agent per team (do not share the same component instance).
            GameObject redObject = Instantiate(playerPrefab, redTeamRoot);
            GameObject blueObject = Instantiate(npcPrefab, blueTeamRoot);
            // Ensure the enemy side is recognized as AI-controlled in offline demo.
            if (blueObject.GetComponent<NPCBehavior>() == null) {
                blueObject.AddComponent<NPCBehavior>();
            }
            Agent redAgent = redObject.GetComponent<Agent>();
            Agent blueAgent = blueObject.GetComponent<Agent>();
            if (redAgent == null || blueAgent == null) {
                Debug.LogError("GameplayDemo: Agent prefab is missing Agent component.");
                return;
            }
            redTeam.Add(redAgent);
            blueTeam.Add(blueAgent);
            gridMap.ReinitializeMapManager(redTeam, blueTeam, gridMap.RedSpawnPoints, gridMap.BlueSpawnPoints);
        }

        Transform FindTeamRoot(string teamName) {
            if (gridMap != null && gridMap.transform != null) {
                var underGrid = gridMap.transform.Find(teamName);
                if (underGrid != null) return underGrid;
            }
            var global = GameObject.Find(teamName);
            if (global != null) return global.transform;
            // Ensure team roots exist so NPCBehavior team detection via ParentName works.
            var created = new GameObject(teamName);
            if (gridMap != null)
                created.transform.SetParent(gridMap.transform, false);
            return created.transform;
        }

        void DestroyDefaultTeamAgents() {
            foreach (var a in gridMap.RedAgents) {
                if (a != null) Destroy(a.gameObject);
            }
            foreach (var a in gridMap.BlueAgents) {
                if (a != null) Destroy(a.gameObject);
            }
        }
    }
}
