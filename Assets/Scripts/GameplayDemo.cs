using UnityEngine;
using NetFlower.UI;
using System.Collections.Generic;

namespace NetFlower {
    /// <summary>
    /// Bootstraps the battle demo. Waits for GridMap to finish initializing,
    /// then tells BattleManager to start.
    /// When PersistentPlayerPreferences.isPlayingOnline is true, replaces BattleManager
    /// with OnlineBattleManager (same settings) so the battle uses the server WebSocket.
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
        /// Online PvP: destroy scene default agents, then spawn one prefab per lobby slot on each team so every
        /// client builds the same BattleManager turn order and stable network unit ids (r0, r1, … / b0, …).
        /// Uses Match.CommitFromLobby roster (player ids + character ids). Human slots have no
        /// NPCBehavior; slots with no human player id (&lt;= 0) get NPC for host-driven AI.
        /// If lobby data is missing, falls back to the old two-prefab 1v1 bootstrap.
        /// </summary>
        void OnlineFillGridMap(PersistentPlayerPreferences prefs) {
            if (allAgents == null) {
                allAgents = Resources.Load<AllAgents>("Settings/AllAgents");
            }

            var match = Match.GetInstance();
            int redSlots = MaxRosterSlotCount(match?.lobbyRedPlayerIds, match?.lobbyRedCharacterIds);
            int blueSlots = MaxRosterSlotCount(match?.lobbyBluePlayerIds, match?.lobbyBlueCharacterIds);

            // Need both teams and committed lobby roster for multi-unit online battles.
            if (match == null || redSlots < 1 || blueSlots < 1) {
                OnlineFillGridMapTwoPlayerFallback(prefs, match);
                return;
            }

            var redTeam = BuildOnlineTeamAgents(redTeamRoot, match.lobbyRedPlayerIds, match.lobbyRedCharacterIds, redSlots);
            var blueTeam = BuildOnlineTeamAgents(blueTeamRoot, match.lobbyBluePlayerIds, match.lobbyBlueCharacterIds, blueSlots);
            if (redTeam.Count < 1 || blueTeam.Count < 1) {
                foreach (var a in redTeam) {
                    if (a != null) Destroy(a.gameObject);
                }
                foreach (var a in blueTeam) {
                    if (a != null) Destroy(a.gameObject);
                }
                Debug.LogError($"GameplayDemo (online): Failed to build teams from lobby roster (red={redTeam.Count}, blue={blueTeam.Count}). Check AllAgents prefabs and lobby character ids.");
                return;
            }

            DestroyDefaultTeamAgents();

            Debug.Log($"[GameplayDemo] Online lobby roster: redSlots={redSlots} blueSlots={blueSlots} -> spawned red={redTeam.Count} blue={blueTeam.Count}");
            gridMap.ReinitializeMapManager(redTeam, blueTeam, gridMap.RedSpawnPoints, gridMap.BlueSpawnPoints);
        }

        /// <summary>Legacy 1v1 when Match has no roster (e.g. launching battle scene without lobby).</summary>
        void OnlineFillGridMapTwoPlayerFallback(PersistentPlayerPreferences prefs, Match match) {
            if (allAgents == null) {
                allAgents = Resources.Load<AllAgents>("Settings/AllAgents");
            }

            bool localOnRed = match == null || match.selectedTeam == Match.TeamSelection.Red;

            int opponentCharId = -1;
            if (match != null) {
                var oppChars = localOnRed ? match.lobbyBlueCharacterIds : match.lobbyRedCharacterIds;
                if (oppChars != null && oppChars.Length > 0)
                    opponentCharId = oppChars[0];
            }

            GameObject localPrefab = allAgents != null ? allAgents.GetAgentPrefabById(prefs.characterId) : null;
            GameObject opponentPrefab = ResolveCharacterPrefab(allAgents, opponentCharId);

            if (localPrefab == null) {
                Debug.LogError($"GameplayDemo (online fallback): Selected player prefab is null. characterId={prefs?.characterId}");
                return;
            }
            if (opponentPrefab == null) {
                Debug.LogError("GameplayDemo (online fallback): Opponent prefab is null in AllAgents.");
                return;
            }

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

            ConfigureOnlineHumanSlot(redObject);
            ConfigureOnlineHumanSlot(blueObject);

            Agent redAgent = redObject.GetComponent<Agent>();
            Agent blueAgent = blueObject.GetComponent<Agent>();
            if (redAgent == null || blueAgent == null) {
                Debug.LogError("GameplayDemo (online fallback): Agent prefab is missing Agent component.");
                return;
            }

            var redTeam = new List<Agent> { redAgent };
            var blueTeam = new List<Agent> { blueAgent };
            gridMap.ReinitializeMapManager(redTeam, blueTeam, gridMap.RedSpawnPoints, gridMap.BlueSpawnPoints);
        }

        static int MaxRosterSlotCount(int[] playerIds, int[] characterIds) {
            int n = 0;
            if (playerIds != null && playerIds.Length > n) n = playerIds.Length;
            if (characterIds != null && characterIds.Length > n) n = characterIds.Length;
            return n;
        }

        static int SafeArrayGet(int[] arr, int index, int defaultValue) {
            if (arr == null || index < 0 || index >= arr.Length) return defaultValue;
            return arr[index];
        }

        /// <summary>Prefab for a lobby character id without spamming errors for unknown ids.</summary>
        static GameObject ResolveCharacterPrefab(AllAgents all, int characterId) {
            if (all == null) return null;
            if (characterId == all.harpyId) return all.harpyAgent;
            if (characterId == all.elfId) return all.elfAgent;
            if (characterId < 0)
                return all.harpyAgent != null ? all.harpyAgent : all.elfAgent;
            Debug.LogWarning($"[GameplayDemo] Unknown characterId={characterId}; using harpy fallback. Extend AllAgents when you add more characters.");
            return all.harpyAgent != null ? all.harpyAgent : all.elfAgent;
        }

        List<Agent> BuildOnlineTeamAgents(Transform teamRoot, int[] lobbyPlayerIds, int[] lobbyCharacterIds, int slotCount) {
            var team = new List<Agent>(slotCount);
            for (int i = 0; i < slotCount; i++) {
                int charId = SafeArrayGet(lobbyCharacterIds, i, -1);
                bool hasPlayerEntry = lobbyPlayerIds != null && i < lobbyPlayerIds.Length;
                // Only treat as NPC when the lobby explicitly lists this slot with no human (id <= 0).
                // If player-id array is shorter than character roster, assume human slots (do not default to 0 -> all bots).
                bool isNpcSlot = hasPlayerEntry && lobbyPlayerIds[i] <= 0;
                GameObject prefab = ResolveCharacterPrefab(allAgents, charId);
                if (prefab == null) {
                    Debug.LogError($"GameplayDemo (online): No prefab for roster index {i} (characterId={charId}).");
                    continue;
                }

                var go = Instantiate(prefab, teamRoot);
                // Unclaimed / bot slots: host runs NPCBehavior (see OnlineBattleManager.LocalMayControlCurrentAgent).
                if (isNpcSlot) {
                    if (go.GetComponent<NPCBehavior>() == null)
                        go.AddComponent<NPCBehavior>();
                } else {
                    ConfigureOnlineHumanSlot(go);
                }

                var agent = go.GetComponent<Agent>();
                if (agent == null) {
                    Debug.LogError($"GameplayDemo (online): Prefab {prefab.name} has no Agent component.");
                    Destroy(go);
                    continue;
                }
                team.Add(agent);
            }
            return team;
        }

        static void ConfigureOnlineHumanSlot(GameObject agentObject) {
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
