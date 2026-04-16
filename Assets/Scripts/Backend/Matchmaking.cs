using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NativeWebSocket;
using UnityEngine;
using UnityEngine.SceneManagement;
using NetFlower;

namespace NetFlower.Backend {
    /// <summary>
    /// Lobby phase only: lobby-control WebSocket actions + snapshot updates.
    /// When everyone is ready, commits roster into <see cref="Match"/> for the battle scene.
    /// Put this on your Lobby scene (not necessarily DontDestroyOnLoad).
    /// </summary>
    public class Matchmaking : MonoBehaviour {
        [Serializable]
        class LobbyControlAction {
            public string action;
            public int maxPlayers;
            public int matchId;
            public string team;
            public int characterId;
        }

        [Serializable]
        class LobbyControlEvent {
            public string type;
            public int playerId;
            public int matchId;
            public string detail;
        }

        /// <summary>JsonUtility-friendly snapshot (arrays, not List).</summary>
        [Serializable]
        public class LobbyState {
            public bool everyoneReady;
            public string lobbyStatus;
            public int[] redTeamPlayerIds;
            public int[] blueTeamPlayerIds;
            public int[] redTeamCharacterIds;
            public int[] blueTeamCharacterIds;
        }

        [Tooltip("REST base, no trailing slash. Should match Login / Match.")]
        [SerializeField] string httpApiBaseUrl = "https://litecoders.com/api";

        [Tooltip("Lobby WebSocket base (no trailing slash). Empty: derived from HTTP + /ws.")]
        [SerializeField] string lobbyWebSocketBaseUrl = "";

        [Tooltip("Maximum players used when requesting joinNewLobby over WebSocket.")]
        [SerializeField] int lobbyMaxPlayers = 8;

        [Tooltip("Scene to load after the countdown when everyone is ready.")]
        [SerializeField] string battleSceneName = "";

        [Tooltip("Seconds to wait after everyone readies up before loading the battle scene.")]
        [SerializeField] float countdownSeconds = 5f;

        [SerializeField] TMPro.TextMeshProUGUI redTeamText;
        [SerializeField] TMPro.TextMeshProUGUI blueTeamText;

        [Tooltip("Optional: displays the countdown. Leave empty to skip UI text.")]
        [SerializeField] TMPro.TextMeshProUGUI countdownText;

        string _authToken;
        Match _match;

        WebSocket _lobbySocket;
        CancellationTokenSource _lobbyCts;
        readonly ConcurrentQueue<string> _lobbyIncoming = new ConcurrentQueue<string>();
        readonly SemaphoreSlim _lobbySendLock = new SemaphoreSlim(1, 1);
        bool _matchStartRequested;

        string EffectiveApiBase() => GameApiEndpoints.EffectiveApiBase(httpApiBaseUrl);

        void Start() {
            _match = Match.PersistentInstance;
            if (_match == null) {
                Debug.LogError("[Matchmaking] No Match singleton. Add a Match component to a DontDestroyOnLoad object before the Lobby scene.");
                return;
            }

            _authToken = PlayerPrefs.GetString("auth_token", "");
            // _authToken = PersistentPlayerPreferences.instance.authToken;
            if (string.IsNullOrEmpty(_authToken)) {
                Debug.LogError("[Matchmaking] No auth token found. Player must log in before entering the lobby.");
                return;
            }

            var wsBase = GameApiEndpoints.LobbyWebSocketBaseTrimmed(EffectiveApiBase(), lobbyWebSocketBaseUrl);
            Debug.Log($"[Matchmaking] Lobby control WebSocket base: \"{wsBase}\"");
            ConnectLobbyWebSocket();
        }

        void Update() {
            DrainLobbyJsonQueue();
        }

        /// <summary>Process messages from WebSocket on the main thread (safe for TMP/UI).</summary>
        void DrainLobbyJsonQueue() {
            while (_lobbyIncoming.TryDequeue(out var json)) {
                if (string.IsNullOrWhiteSpace(json))
                    continue;
                HandleLobbyMessage(json);
            }
        }

        void OnDestroy() {
            DisconnectLobbyWebSocket();
        }

        /// <summary>Leave the current lobby via websocket action.</summary>
        public void LeaveLobby() {
            if (_match != null && _match.dbMatchId > 0 && !_matchStartRequested)
                _ = SendLobbyActionAsync("leaveLobby");
        }

        static string IdsToStr(int[] ids) {
            if (ids == null || ids.Length == 0) return "";
            return string.Join(",", ids);
        }

        void UpdateLobbyGui(LobbyState lobbyState) {
            if (redTeamText != null)
                redTeamText.text = "Red Team: " + IdsToStr(lobbyState.redTeamPlayerIds);
            if (blueTeamText != null)
                blueTeamText.text = "Blue Team: " + IdsToStr(lobbyState.blueTeamPlayerIds);

            if (_matchStartRequested)
                return;

            bool teamsPopulated = lobbyState.redTeamPlayerIds != null && lobbyState.redTeamPlayerIds.Length > 0
                               && lobbyState.blueTeamPlayerIds != null && lobbyState.blueTeamPlayerIds.Length > 0;

            if (lobbyState.everyoneReady && teamsPopulated) {
                _matchStartRequested = true;
                Debug.Log($"[Matchmaking] Everyone ready - lobby locked (status={lobbyState.lobbyStatus}). Starting countdown.");
                DisconnectLobbyWebSocket();
                StartCoroutine(CountdownThenLoadBattle(lobbyState));
            }
        }

        IEnumerator CountdownThenLoadBattle(LobbyState lobbyState) {
            float remaining = Mathf.Max(countdownSeconds, 0f);

            while (remaining > 0f) {
                int display = Mathf.CeilToInt(remaining);
                if (countdownText != null)
                    countdownText.text = $"Match starting in {display}...";
                Debug.Log($"[Matchmaking] Match starting in {display}...");
                yield return new WaitForSeconds(1f);
                remaining -= 1f;
            }

            if (countdownText != null)
                countdownText.text = "GO!";

            _match.CommitFromLobby(_match.dbMatchId, lobbyState.redTeamPlayerIds, lobbyState.blueTeamPlayerIds,
                lobbyState.redTeamCharacterIds, lobbyState.blueTeamCharacterIds);

            if (!string.IsNullOrEmpty(battleSceneName)) {
                Debug.Log($"[Matchmaking] Loading scene \"{battleSceneName}\"");
                SceneManager.LoadScene(battleSceneName);
            } else {
                Debug.LogWarning("[Matchmaking] battleSceneName is empty - skipping scene load. Set it in the Inspector.");
            }
        }

        public void PressJoinRedTeam() {
            _match.SetSelectedTeam(Match.TeamSelection.Red);
            _ = SendLobbyActionAsync("setTeam", team: "red");
        }

        public void PressJoinBlueTeam() {
            _match.SetSelectedTeam(Match.TeamSelection.Blue);
            _ = SendLobbyActionAsync("setTeam", team: "blue");
        }

        public void PressReadyButton() {
            _ = SendLobbyActionAsync("setReady");
        }

        void ConnectLobbyWebSocket() {
            DisconnectLobbyWebSocket();
            _lobbyCts = new CancellationTokenSource();
            var wsBase = GameApiEndpoints.LobbyWebSocketBaseTrimmed(EffectiveApiBase(), lobbyWebSocketBaseUrl);
            var url = $"{wsBase}/lobby-control?authToken={Uri.EscapeDataString(_authToken)}";
            var socket = new WebSocket(url);
            _lobbySocket = socket;

            socket.OnOpen += () => {
                Debug.Log($"[Matchmaking] Lobby control WebSocket connected {url}");
                if (_match != null && _match.dbMatchId > 0)
                    _ = SendLobbyActionAsync("subscribeLobby", matchId: _match.dbMatchId);
                else {
                    var prefs = PersistentPlayerPreferences.instance;
                    int charId = prefs != null ? prefs.characterId : 0;
                    _ = SendLobbyActionAsync("joinNewLobby", maxPlayers: lobbyMaxPlayers, characterId: charId);
                }
            };

            socket.OnMessage += bytes => {
                if (bytes == null || bytes.Length == 0) return;
                _lobbyIncoming.Enqueue(Encoding.UTF8.GetString(bytes));
            };

            socket.OnError += msg => {
                Debug.LogWarning($"[Matchmaking] Lobby WebSocket error: {msg}");
            };

            socket.OnClose += code => {
                Debug.Log($"[Matchmaking] Lobby WebSocket closed: {code}");
                if (ReferenceEquals(_lobbySocket, socket))
                    _lobbySocket = null;
            };

            _ = RunLobbyConnectAsync(socket);
        }

        void DisconnectLobbyWebSocket() {
            _lobbyCts?.Cancel();
            var ws = _lobbySocket;
            _lobbySocket = null;
            _lobbyCts = null;
            if (ws == null) return;
            try {
                ws.CancelConnection();
            } catch { /* ignore */ }
            try {
                _ = ws.Close();
            } catch { /* ignore */ }
        }

        async Task RunLobbyConnectAsync(WebSocket socket) {
            try {
                await socket.Connect();
            } catch (OperationCanceledException) { }
            catch (Exception e) {
                Debug.LogWarning($"[Matchmaking] Lobby WebSocket connect failed: {e.Message}");
                if (ReferenceEquals(_lobbySocket, socket))
                    _lobbySocket = null;
            }
        }

        void HandleLobbyMessage(string json) {
            try {
                var evt = JsonUtility.FromJson<LobbyControlEvent>(json);
                if (evt != null && !string.IsNullOrEmpty(evt.type)) {
                    HandleLobbyControlEvent(evt);
                    return;
                }

                var lobbyState = JsonUtility.FromJson<LobbyState>(json);
                if (lobbyState != null)
                    UpdateLobbyGui(lobbyState);
            } catch (Exception e) {
                Debug.LogWarning($"[Matchmaking] Bad lobby JSON: {e.Message}\\n{json}");
            }
        }

        void HandleLobbyControlEvent(LobbyControlEvent evt) {
            if (evt.type == "connected") {
                Debug.Log($"[Matchmaking] lobby-control connected as player {evt.playerId}");
                return;
            }

            if (evt.type == "joinedLobby") {
                _match.dbMatchId = evt.matchId;
                Debug.Log($"[Matchmaking] Joined lobby matchId={evt.matchId}");
                _ = SendLobbyActionAsync("snapshot");
                return;
            }

            if (evt.type == "subscribed") {
                _match.dbMatchId = evt.matchId;
                Debug.Log($"[Matchmaking] Subscribed to lobby matchId={evt.matchId}");
                _ = SendLobbyActionAsync("snapshot");
                return;
            }

            if (evt.type == "leftLobby") {
                Debug.Log($"[Matchmaking] Left lobby matchId={evt.matchId}");
                if (_match != null && _match.dbMatchId == evt.matchId)
                    _match.dbMatchId = 0;
                return;
            }

            if (evt.type == "error") {
                Debug.LogWarning($"[Matchmaking] Lobby control error: {evt.detail}");
                return;
            }
        }

        async Task<bool> SendLobbyActionAsync(
            string action,
            int matchId = 0,
            string team = null,
            int maxPlayers = 0,
            int characterId = 0
        ) {
            if (_lobbySocket == null || _lobbySocket.State != WebSocketState.Open) {
                Debug.LogWarning($"[Matchmaking] Cannot send lobby action '{action}' - socket not connected");
                return false;
            }

            var payload = new LobbyControlAction {
                action = action,
                matchId = matchId,
                team = team,
                maxPlayers = maxPlayers,
                characterId = characterId,
            };

            var json = JsonUtility.ToJson(payload);

            await _lobbySendLock.WaitAsync();
            try {
                await _lobbySocket.SendText(json);
                return true;
            } catch (Exception e) {
                Debug.LogWarning($"[Matchmaking] Failed to send lobby action '{action}': {e.Message}");
                return false;
            } finally {
                _lobbySendLock.Release();
            }
        }
    }
}
